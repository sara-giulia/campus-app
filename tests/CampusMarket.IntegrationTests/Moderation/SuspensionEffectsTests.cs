using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Features.Auth.Refresh;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.Api.Features.Members.GetMyProfile;
using CampusMarket.Api.Features.Members.GetPublicProfile;
using CampusMarket.Api.Features.Moderation.SuspendMember;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CampusMarket.IntegrationTests.Moderation;

// What a suspension does to the member's account and listings, end to end on PostgreSQL.
public sealed class SuspensionEffectsTests : IntegrationTestBase
{
    private const string Password = "secret12";
    private const string Reason = "Inselatorie repetata";
    private const string EventsUrl = "/api/v1/members/me/events";
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan EventExpected = TimeSpan.FromSeconds(5);

    // ---------- login, refresh and access tokens ----------

    [Fact]
    public async Task Given_SuspendedMember_When_LoggingInWithTheRightPassword_Then_Returns403AccountSuspendedAndNoRefreshToken()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("ion@student.ro", Password), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.AccountSuspended");
        error.Type.Should().Be(ErrorType.Forbidden);
        (await QueryDbAsync(db => db.Set<RefreshToken>().CountAsync(t => t.MemberId == ion.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspendedMember_When_LoggingInWithAWrongPassword_Then_Returns401InvalidCredentials()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("ion@student.ro", "wrong-password"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidCredentials");
        (await QueryDbAsync(db => db.Set<RefreshToken>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_RefreshTokenOfAMemberSuspendedThroughTheApi_When_Refreshing_Then_Returns401AndTheTokenIsRevoked()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        var token = RefreshToken.Create(ion.Id, "ion-refresh-token", DateTimeOffset.UtcNow);
        await SeedAsync(ion, token);
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest("ion-refresh-token"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.InvalidRefreshToken");
        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_ActiveRefreshTokenOfASuspendedMember_When_Refreshing_Then_Returns401AccountSuspendedAndRevokesIt()
    {
        // Arrange: a token created after the suspension (the suspension itself revokes the older ones).
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        ion.Suspend(AdminId, Reason, null, DateTimeOffset.UtcNow.AddMinutes(-5));
        var token = RefreshToken.Create(ion.Id, "ion-refresh-token", DateTimeOffset.UtcNow);
        await SeedAsync(ion, token);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest("ion-refresh-token"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.AccountSuspended");
        error.Type.Should().Be(ErrorType.Forbidden);
        var saved = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync());
        saved.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_AccessTokenIssuedBeforeTheSuspension_When_CallingAnAuthenticatedEndpoint_Then_Returns401AccountSuspended()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var oldToken = TestTokens.Create(ion.Id);
        await SuspendThroughApiAsync(ion.Id);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.WwwAuthenticate.ToString().Should().Contain("invalid_token");
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.AccountSuspended");
        error.Type.Should().Be(ErrorType.Forbidden);
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_AccessTokenOfASuspendedMember_When_CallingAPublicEndpoint_Then_TheRequestIsAnonymous()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        var dan = Member.Create("Dan Pop", "dan@student.ro", CreatedAt).Value;
        var danListing = ActiveListing(dan.Id, "Calculator stiintific");
        await SeedAsync(ion, dan, danListing);
        var oldToken = TestTokens.Create(ion.Id);
        await SuspendThroughApiAsync(ion.Id);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(danListing.Id);
        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(1);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(null)]
    public async Task Given_ValidTokenWithoutAGuidSubject_When_CallingAPublicEndpoint_Then_TheSuspensionCheckIsSkipped(string? subject)
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenWithSubject(subject, TestTokens.Key));

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json))!.TotalCount.Should().Be(0);
        (await QueryDbAsync(db => db.Set<Member>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_TokenSignedWithAnotherKey_When_CallingAnAuthenticatedEndpoint_Then_Returns401ProblemDetailsAsBefore()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TokenWithSubject(ion.Id.ToString(), "another-key-that-is-long-enough-0123456789"));

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Unauthorized");
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().BeNull();
    }

    // ---------- the suspension cache ----------

    [Fact]
    public async Task Given_SuspensionWrittenToTheDatabaseAfterARequest_When_TheCacheIsNotInvalidated_Then_TheOldTokenStillWorks()
    {
        // Arrange: the first authenticated request fills the cache; the suspension is then saved without the endpoint.
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(ion.Id);
        (await Client.GetAsync("/api/v1/members/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        await SuspendInTheDatabaseAsync(ion.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the suspension check answers from its 30-second cache");
        (await response.Content.ReadFromJsonAsync<GetMyProfileResponse>(Json))!.Id.Should().Be(ion.Id);
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_SuspensionWrittenToTheDatabaseAfterARequest_When_TheCacheIsInvalidated_Then_TheOldTokenIsRejected()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(ion.Id);
        (await Client.GetAsync("/api/v1/members/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        await SuspendInTheDatabaseAsync(ion.Id);
        InvalidateSuspensionCache(Factory);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<Error>(Json))!.Code.Should().Be("Auth.AccountSuspended");
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_ANullCacheEntry_When_CallingAnAuthenticatedEndpoint_Then_TheSuspendedIdsAreReadAgain()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        ion.Suspend(AdminId, Reason, null, DateTimeOffset.UtcNow.AddMinutes(-5));
        await SeedAsync(ion);
        Factory.Services.GetRequiredService<IMemoryCache>().Set<IReadOnlySet<Guid>?>(CachedSuspensionCheck.CacheKey, null);
        AuthenticateAs(ion.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<Error>(Json))!.Code.Should().Be("Auth.AccountSuspended");
        Factory.Services.GetRequiredService<IMemoryCache>()
            .Get<IReadOnlySet<Guid>>(CachedSuspensionCheck.CacheKey).Should().BeEquivalentTo(new[] { ion.Id });
    }

    // ---------- the member's listings ----------

    [Fact]
    public async Task Given_SuspendedSeller_When_SearchingListings_Then_TheirListingsAreNotReturned()
    {
        // Arrange
        var (ion, ionListing, dan, danListing) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(danListing.Id);
        body.TotalCount.Should().Be(1);
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_GettingSimilarListingsOfAnotherListing_Then_TheirListingIsNotSuggested()
    {
        // Arrange
        var ion = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var dan = Member.Create("Dan Pop", "dan@student.ro", CreatedAt).Value;
        var target = ActiveListing(dan.Id, "Manual Analiza matematica, editia 2", "Manual pentru anul I, cu exercitii rezolvate.");
        var danOther = ActiveListing(dan.Id, "Notite Analiza matematica, anul I", "Notite de curs, exercitii rezolvate.", 1);
        var ionListing = ActiveListing(ion.Id, "Culegere Analiza matematica, exercitii rezolvate", "Culegere pentru examen, exercitii rezolvate.", 2);
        await SeedAsync(ion, dan, target, danOther, ionListing);
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{target.Id}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetSimilarListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Contain(danOther.Id).And.NotContain(ionListing.Id);
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_GettingSimilarListingsOfTheirListing_Then_Returns404()
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{ionListing.Id}/similar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<Error>(Json))!.Code.Should().Be("Listing.NotFound");
        await AssertListingUnchangedAsync(ionListing);
    }

    [Theory]
    [InlineData("/api/v1/listings/{0}")]
    [InlineData("/api/v1/listings/{0}/photos")]
    public async Task Given_SuspendedSeller_When_AnotherMemberReadsTheirListing_Then_Returns404(string urlFormat)
    {
        // Arrange
        var (ion, ionListing, dan, _) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(dan.Id);

        // Act
        var response = await Client.GetAsync(string.Format(urlFormat, ionListing.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_AnonymousReadsTheirListing_Then_Returns404()
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{ionListing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<Error>(Json))!.Code.Should().Be("Listing.NotFound");
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_AdminReadsTheirListing_Then_Returns200()
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{ionListing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingByIdResponse>(Json);
        body!.Id.Should().Be(ionListing.Id);
        body.SellerId.Should().Be(ion.Id);
        body.Status.Should().Be(ListingStatus.Active);
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_SuspendedMember_When_AnotherMemberReadsTheirPublicProfile_Then_Returns404()
    {
        // Arrange
        var (ion, ionListing, dan, _) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(dan.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{ion.Id}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_SuspendedMember_When_AdminReadsTheirPublicProfile_Then_Returns200WithTheirListings()
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync($"/api/v1/members/{ion.Id}/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetPublicProfileResponse>(Json);
        body!.Id.Should().Be(ion.Id);
        body.Name.Should().Be("Ion Ionescu");
        body.ActiveListings.Select(l => l.Id).Should().Equal(ionListing.Id);
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_FavoriteOfASuspendedSeller_When_GettingMyFavorites_Then_ItIsNotShownButTheRowStays()
    {
        // Arrange
        var (ion, ionListing, _, danListing) = await SeedTwoSellersAsync();
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(ana,
            Favorite.Create(ana.Id, ionListing, CreatedAt.AddHours(1)).Value,
            Favorite.Create(ana.Id, danListing, CreatedAt.AddHours(2)).Value);
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(ana.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/favorites");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyFavoritesResponse>(Json);
        body!.Items.Select(i => i.ListingId).Should().Equal(danListing.Id);
        body.TotalCount.Should().Be(1);
        (await QueryDbAsync(db => db.Set<Favorite>().CountAsync(f => f.MemberId == ana.Id))).Should().Be(2);
    }

    [Theory]
    [InlineData("/api/v1/listings/{0}/reservations", null)]
    [InlineData("/api/v1/listings/{0}/offers", """{"amount":90}""")]
    [InlineData("/api/v1/members/me/favorites/{0}", null)]
    [InlineData("/api/v1/listings/{0}/conversations", """{"text":"Mai e disponibil?"}""")]
    [InlineData("/api/v1/listings/{0}/reports", """{"reason":"Pare fals"}""")]
    public async Task Given_ListingOfASuspendedSeller_When_AnotherMemberActsOnIt_Then_Returns404AndNothingIsSaved(string urlFormat, string? json)
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(ana);
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(ana.Id);
        using var content = new StringContent(json ?? "", Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync(string.Format(urlFormat, ionListing.Id), json is null ? null : content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        await AssertListingUnchangedAsync(ionListing);
        (await QueryDbAsync(db => db.Set<Offer>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Favorite>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Report>().CountAsync())).Should().Be(0);
    }

    // ---------- conversations ----------

    [Fact]
    public async Task Given_ConversationWithASuspendedMember_When_SendingThemAMessage_Then_Returns409AndNothingIsSaved()
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var conversation = Conversation.Start(ana.Id, ionListing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(ana, conversation);
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(ana.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new { text = "Ma auzi?" }, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.MemberSuspended");
        error.Type.Should().Be(ErrorType.Conflict);
        (await QueryDbAsync(db => db.Set<Message>().CountAsync())).Should().Be(1);
        (await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync())).LastMessageAt.Should().Be(CreatedAt);
    }

    [Fact]
    public async Task Given_ConversationWithASuspendedMember_When_TheOtherMemberReadsIt_Then_Returns200()
    {
        // Arrange
        var (ion, ionListing, _, _) = await SeedTwoSellersAsync();
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var conversation = Conversation.Start(ana.Id, ionListing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(ana, conversation);
        await SuspendThroughApiAsync(ion.Id);
        AuthenticateAs(ana.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetConversationResponse>(Json);
        body!.Id.Should().Be(conversation.Id);
        body.SellerId.Should().Be(ion.Id);
        body.Messages.Should().ContainSingle().Which.Text.Should().Be("Mai e disponibil?");
        (await QueryDbAsync(db => db.Set<Message>().CountAsync())).Should().Be(1);
    }

    // ---------- live events ----------

    [Fact]
    public async Task Given_OpenLiveStream_When_TheMemberIsSuspended_Then_TheStreamIsClosed()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        using var stream = await OpenStreamAsync(ion.Id);
        stream.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadLineAsync(stream.Reader)).Should().Be("event: ping");
        (await ReadLineAsync(stream.Reader)).Should().StartWith("data:");
        (await ReadLineAsync(stream.Reader)).Should().BeEmpty();

        // Act
        await SuspendThroughApiAsync(ion.Id);

        // Assert: the server completes the stream, so the next read reaches its end instead of timing out.
        (await ReadLineAsync(stream.Reader)).Should().BeNull("the suspension closes the member's open streams");
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_SuspendedMember_When_ReconnectingTheLiveStream_Then_Returns401AccountSuspended()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        await SuspendThroughApiAsync(ion.Id);

        // Act
        using var stream = await OpenStreamAsync(ion.Id);

        // Assert
        stream.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stream.Response.Content.ReadFromJsonAsync<Error>(Json))!.Code.Should().Be("Auth.AccountSuspended");
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().NotBeNull();
    }

    // ---------- after the suspension ----------

    [Fact]
    public async Task Given_SuspensionLifted_When_SearchingListings_Then_TheListingsAreBackAndTheCancelledReservationStaysCancelled()
    {
        // Arrange
        var (ion, ionListing, _, danListing) = await SeedReservedListingOfIonAsync();
        await SuspendThroughApiAsync(ion.Id);
        await LiftThroughApiAsync(ion.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { ionListing.Id, danListing.Id });
        body.TotalCount.Should().Be(2);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == ionListing.Id));
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Given_SuspensionLifted_When_LoggingIn_Then_Returns200()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        await SuspendThroughApiAsync(ion.Id);
        await LiftThroughApiAsync(ion.Id);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("ion@student.ro", Password), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!.Token.Should().NotBeNullOrEmpty();
        (await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().SingleAsync())).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_SuspensionLifted_When_UsingAnAccessTokenIssuedBefore_Then_Returns200()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var oldToken = TestTokens.Create(ion.Id);
        await SuspendThroughApiAsync(ion.Id);
        await LiftThroughApiAsync(ion.Id);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<GetMyProfileResponse>(Json))!.Id.Should().Be(ion.Id);
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_TemporarySuspensionThatEnded_When_LoggingIn_Then_Returns200()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var clock = new AdjustableTimeProvider();
        await using var host = WithClock(clock);
        using var client = host.CreateClient();
        await SuspendUntilAsync(client, ion.Id, clock.GetUtcNow().AddMinutes(2));
        clock.Advance(TimeSpan.FromMinutes(3));
        InvalidateSuspensionCache(host);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("ion@student.ro", Password), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!.Token.Should().NotBeNullOrEmpty();
        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.SuspendedAt.Should().NotBeNull("an ended suspension keeps its columns until the next one");
        saved.SuspendedUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_TemporarySuspensionThatEnded_When_UsingAnAccessTokenIssuedBefore_Then_Returns200()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var clock = new AdjustableTimeProvider();
        await using var host = WithClock(clock);
        using var client = host.CreateClient();
        await SuspendUntilAsync(client, ion.Id, clock.GetUtcNow().AddMinutes(2));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(ion.Id));
        (await client.GetAsync("/api/v1/members/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        clock.Advance(TimeSpan.FromMinutes(3));
        InvalidateSuspensionCache(host);

        // Act
        var response = await client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<GetMyProfileResponse>(Json))!.Id.Should().Be(ion.Id);
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_TemporarySuspensionThatEnded_When_SearchingListings_Then_TheSellersListingIsBack()
    {
        // Arrange
        var (ion, ionListing, _, danListing) = await SeedTwoSellersAsync();
        var clock = new AdjustableTimeProvider();
        await using var host = WithClock(clock);
        using var client = host.CreateClient();
        await SuspendUntilAsync(client, ion.Id, clock.GetUtcNow().AddMinutes(2));
        client.DefaultRequestHeaders.Authorization = null;
        clock.Advance(TimeSpan.FromMinutes(3));

        // Act
        var response = await client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { ionListing.Id, danListing.Id });
        await AssertListingUnchangedAsync(ionListing);
    }

    [Fact]
    public async Task Given_TemporarySuspensionThatEnded_When_AdminLiftsIt_Then_Returns409NotSuspended()
    {
        // Arrange
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var clock = new AdjustableTimeProvider();
        await using var host = WithClock(clock);
        using var client = host.CreateClient();
        await SuspendUntilAsync(client, ion.Id, clock.GetUtcNow().AddMinutes(2));
        clock.Advance(TimeSpan.FromMinutes(3));

        // Act
        var response = await client.PostAsync($"/api/v1/moderation/members/{ion.Id}/lift-suspension", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<Error>(Json))!.Code.Should().Be("Member.NotSuspended");
        (await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync())).SuspendedAt.Should().NotBeNull();
    }

    // ---------- helpers ----------

    private static Member MemberWithPassword(string name, string email) =>
        Member.Register(name, email, new AspNetPasswordHasher().Hash(Password), CreatedAt).Value;

    private static Listing ActiveListing(Guid sellerId, string title, string? description = null, int hours = 0)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 40m, CreatedAt.AddHours(hours), description).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    private async Task<(Member Ion, Listing IonListing, Member Dan, Listing DanListing)> SeedTwoSellersAsync()
    {
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        var dan = Member.Create("Dan Pop", "dan@student.ro", CreatedAt).Value;
        var ionListing = ActiveListing(ion.Id, "Manual POO", hours: 1);
        var danListing = ActiveListing(dan.Id, "Calculator stiintific", hours: 2);
        await SeedAsync(ion, dan, ionListing, danListing);
        return (ion, ionListing, dan, danListing);
    }

    private async Task<(Member Ion, Listing IonListing, Member Ana, Listing DanListing)> SeedReservedListingOfIonAsync()
    {
        var ion = MemberWithPassword("Ion Ionescu", "ion@student.ro");
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var dan = Member.Create("Dan Pop", "dan@student.ro", CreatedAt).Value;
        var ionListing = ActiveListing(ion.Id, "Manual POO", hours: 1);
        ionListing.Reserve(ana.Id, DateTimeOffset.UtcNow);
        var danListing = ActiveListing(dan.Id, "Calculator stiintific", hours: 2);
        await SeedAsync(ion, ana, dan, ionListing, danListing);
        return (ion, ionListing, ana, danListing);
    }

    private async Task AssertListingUnchangedAsync(Listing expected)
    {
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == expected.Id));
        saved.Status.Should().Be(expected.Status);
        saved.Version.Should().Be(expected.Version);
        saved.Title.Should().Be(expected.Title);
    }

    // Suspends through the endpoint as the Admin (which also clears the suspension cache), then logs the client out.
    private async Task SuspendThroughApiAsync(Guid memberId)
    {
        AuthenticateAs(AdminId, "Admin");
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/moderation/members/{memberId}/suspend", new SuspendMemberRequest(Reason, null), Json);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Client.DefaultRequestHeaders.Authorization = null;
    }

    private async Task LiftThroughApiAsync(Guid memberId)
    {
        AuthenticateAs(AdminId, "Admin");
        var response = await Client.PostAsync($"/api/v1/moderation/members/{memberId}/lift-suspension", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Client.DefaultRequestHeaders.Authorization = null;
    }

    private static async Task SuspendUntilAsync(HttpClient client, Guid memberId, DateTimeOffset until)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(AdminId, "Admin"));
        var response = await client.PostAsJsonAsync(
            $"/api/v1/moderation/members/{memberId}/suspend", new SuspendMemberRequest(Reason, until), Json);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Saves a suspension without the endpoint, so the suspension cache is not told about it.
    private async Task SuspendInTheDatabaseAsync(Guid memberId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        var member = await db.Set<Member>().SingleAsync(m => m.Id == memberId);
        member.Suspend(AdminId, Reason, null, DateTimeOffset.UtcNow.AddMinutes(-1));
        await db.SaveChangesAsync();
    }

    private static void InvalidateSuspensionCache(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISuspensionCheck>().Invalidate();
    }

    // A copy of the API with a clock the test moves forward.
    private WebApplicationFactory<Program> WithClock(AdjustableTimeProvider clock) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));

    private static string TokenWithSubject(string? subject, string key)
    {
        var claims = new List<Claim> { new("role", "Student") };
        if (subject is not null)
            claims.Add(new Claim("sub", subject));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "CampusMarket",
            Audience = "CampusMarket",
            Expires = DateTime.UtcNow.AddMinutes(30),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });
    }

    private async Task<LiveStream> OpenStreamAsync(Guid memberId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(memberId));
        var response = await client.GetAsync(EventsUrl, HttpCompletionOption.ResponseHeadersRead);
        var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        return new LiveStream(client, response, reader);
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader)
    {
        using var cts = new CancellationTokenSource(EventExpected);
        return await reader.ReadLineAsync(cts.Token);
    }

    private sealed class LiveStream(HttpClient client, HttpResponseMessage response, StreamReader reader) : IDisposable
    {
        public HttpResponseMessage Response { get; } = response;
        public StreamReader Reader { get; } = reader;

        public void Dispose()
        {
            Reader.Dispose();
            Response.Dispose();
            client.Dispose();
        }
    }
}
