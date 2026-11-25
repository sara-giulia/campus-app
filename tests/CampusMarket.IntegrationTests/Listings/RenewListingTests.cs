using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class RenewListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BuyerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static string RenewUrl(Guid id) => $"/api/v1/listings/{id}/renew";

    [Fact]
    public async Task Given_ExpiredListing_When_SellerRenews_Then_Returns204AndListingIsActiveForThirtyMoreDays()
    {
        // Arrange
        var listing = ExpiryListings.Expired(SellerId);
        await SeedAsync(listing);
        AuthenticateAs(SellerId);
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await Client.PostAsync(RenewUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
        saved.ExpiresAt.Should().BeCloseTo(before.AddDays(30), TimeSpan.FromMinutes(5));
        saved.Version.Should().Be(listing.Version + 1);

        var search = await Client.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        search!.Items.Select(i => i.Id).Should().Equal(listing.Id);
    }

    [Fact]
    public async Task Given_CachedSearchWithoutTheListing_When_SellerRenews_Then_SameSearchReturnsTheListing()
    {
        // Arrange
        var listing = ExpiryListings.Expired(SellerId);
        await SeedAsync(listing);
        using var anonymous = Factory.CreateClient();
        var before = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);
        before!.Items.Should().BeEmpty();
        AuthenticateAs(SellerId);

        // Act
        var renewed = await Client.PostAsync(RenewUrl(listing.Id), content: null);
        var after = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings", Json);

        // Assert
        renewed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after!.Items.Select(i => i.Id).Should().Equal(listing.Id);
    }

    [Fact]
    public async Task Given_NoToken_When_Renewing_Then_Returns401AndKeepsListingExpired()
    {
        // Arrange
        var listing = ExpiryListings.Expired(SellerId);
        await SeedAsync(listing);

        // Act
        var response = await Client.PostAsync(RenewUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(RenewUrl(listing.Id));

        await AssertUnchangedAsync(listing);
    }

    [Fact]
    public async Task Given_UnknownId_When_Renewing_Then_Returns404()
    {
        // Arrange
        var listing = ExpiryListings.Expired(SellerId);
        await SeedAsync(listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(RenewUrl(Guid.NewGuid()), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertUnchangedAsync(listing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NotTheSeller_When_Renewing_Then_Returns403AndKeepsListingExpired(bool isAdmin)
    {
        // Arrange
        var listing = ExpiryListings.Expired(SellerId);
        await SeedAsync(listing);
        if (isAdmin)
            AuthenticateAs(AdminId, "Admin");
        else
            AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsync(RenewUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertUnchangedAsync(listing);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Active)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Hidden)]
    public async Task Given_ListingThatIsNotExpired_When_SellerRenews_Then_Returns400NotExpiredAndKeepsListing(ListingStatus status)
    {
        // Arrange
        var listing = ExpiryListings.InStatus(SellerId, status, BuyerId);
        await SeedAsync(listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(RenewUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotExpired");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(status);
        saved.Version.Should().Be(listing.Version);
        saved.ExpiresAt.Should().Be(listing.ExpiresAt.ToMicroseconds());
    }

    [Fact]
    public async Task Given_ExpiredListingInInactiveCategory_When_SellerRenews_Then_Returns400CategoryInactiveAndKeepsListingExpired()
    {
        // Arrange
        var listing = ExpiryListings.Expired(SellerId);
        await SeedAsync(listing);
        await DeactivateAsync(Category.Textbook);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(RenewUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CategoryInactive");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertUnchangedAsync(listing);
    }

    private async Task AssertUnchangedAsync(Listing seeded)
    {
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Expired);
        saved.Version.Should().Be(seeded.Version);
        saved.ExpiresAt.Should().Be(seeded.ExpiresAt.ToMicroseconds());
    }

    private async Task DeactivateAsync(Category key)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        var all = await db.Set<ListingCategory>().ToListAsync();
        var category = all.Single(c => c.Key == key);

        category.Update(category.DisplayName, category.Description, isActive: false, all).IsSuccess.Should().BeTrue();

        await db.SaveChangesAsync();
    }
}
