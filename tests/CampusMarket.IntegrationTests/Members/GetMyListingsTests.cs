using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Members.GetMyListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class GetMyListingsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BuyerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Listing BuildDraft(Guid sellerId, string title, DateTimeOffset createdAt) =>
        Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 50m, createdAt).Value;

    [Fact]
    public async Task Given_NoToken_When_GettingMyListings_Then_Returns401()
    {
        // Arrange
        await SeedAsync(BuildDraft(SellerId, "Manual POO", CreatedAt));

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me/listings");

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_TwoMembersWithListings_When_SellerGetsMyListings_Then_ReturnsOnlyTheSellersListings()
    {
        // Arrange
        var first = BuildDraft(SellerId, "Manual POO", CreatedAt);
        var second = BuildDraft(SellerId, "Calculator stiintific", CreatedAt.AddHours(1));
        var other = BuildDraft(OtherStudentId, "Notite analiza", CreatedAt.AddHours(2));
        await SeedAsync(first, second, other);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyListingsResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Select(i => i.Id).Should().Equal(second.Id, first.Id);
        body.Items.Should().OnlyContain(i => i.SellerId == SellerId);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_TwoMembersWithListings_When_OtherStudentGetsMyListings_Then_ReturnsOnlyTheirOwnListing()
    {
        // Arrange
        var first = BuildDraft(SellerId, "Manual POO", CreatedAt);
        var second = BuildDraft(SellerId, "Calculator stiintific", CreatedAt.AddHours(1));
        var other = BuildDraft(OtherStudentId, "Notite analiza", CreatedAt.AddHours(2));
        await SeedAsync(first, second, other);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyListingsResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items.Select(i => i.Id).Should().Equal(other.Id);
        body.Items[0].Title.Should().Be("Notite analiza");

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_NewDraftListingAfterAFirstCall_When_GettingMyListingsAgain_Then_ReturnsItImmediately()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var firstResponse = await Client.GetAsync("/api/v1/members/me/listings");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var draft = BuildDraft(SellerId, "Manual POO", CreatedAt);
        await SeedAsync(draft);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyListingsResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items[0].Id.Should().Be(draft.Id);
        body.Items[0].Status.Should().Be(ListingStatus.Draft);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_HiddenAndReservedListings_When_GettingMyListings_Then_ReturnsBothWithTheirStatus()
    {
        // Arrange
        var hidden = BuildDraft(SellerId, "Manual POO", CreatedAt);
        hidden.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        hidden.Hide();

        var reserved = BuildDraft(SellerId, "Calculator stiintific", CreatedAt.AddHours(1));
        reserved.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        reserved.Reserve(BuyerId, CreatedAt.AddHours(1));

        await SeedAsync(hidden, reserved);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyListingsResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Select(i => (i.Id, i.Status)).Should().Equal(
            (reserved.Id, ListingStatus.Reserved),
            (hidden.Id, ListingStatus.Hidden));

        var statuses = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().OrderBy(l => l.Title).Select(l => l.Status).ToListAsync());
        statuses.Should().Equal(ListingStatus.Reserved, ListingStatus.Hidden);
    }

    [Fact]
    public async Task Given_ThreeListings_When_GettingMyListings_Then_ReturnsNewestFirst()
    {
        // Arrange
        var oldest = BuildDraft(SellerId, "Oldest", CreatedAt);
        var middle = BuildDraft(SellerId, "Middle", CreatedAt.AddHours(1));
        var newest = BuildDraft(SellerId, "Newest", CreatedAt.AddHours(2));
        await SeedAsync(oldest, middle, newest);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyListingsResponse>(Json);
        body!.Count.Should().Be(3);
        body.Items.Select(i => i.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(3);
    }
}
