using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Blocks;

// The blocker does not see the blocked member's listings in the search, in the similar listings and in the favorites.
public sealed class BlockedVisibilityTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _third = NewMember("Maria Ionescu", "maria@student.ro");
    private readonly Listing _sellersListing;
    private readonly Listing _buyersListing;

    public BlockedVisibilityTests()
    {
        _sellersListing = ActiveListing(_seller.Id, "Manual de algebra liniara");
        _buyersListing = ActiveListing(_buyer.Id, "Culegere de algebra liniara");
    }

    private Task<GetListingsResponse?> SearchAsync(HttpClient client, string search) =>
        client.GetFromJsonAsync<GetListingsResponse>($"/api/v1/listings?search={Uri.EscapeDataString(search)}&pageSize=100", Json);

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_SellerSearches_Then_BuyersListingIsLeftOutOfItemsAndTotal()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _third, _sellersListing, _buyersListing, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        AuthenticateAs(_seller.Id);

        // Act
        var result = await SearchAsync(Client, "algebra");

        // Assert
        result!.Items.Select(i => i.Id).Should().Equal(_sellersListing.Id);
        result.Count.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_OthersSearch_Then_BuyersListingIsStillThere()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _third, _sellersListing, _buyersListing, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        using var anonymous = Factory.CreateClient();
        AuthenticateAs(_buyer.Id);

        // Act
        var asBuyer = await SearchAsync(Client, "algebra");
        var asAnonymous = await SearchAsync(anonymous, "algebra");
        AuthenticateAs(_third.Id);
        var asThird = await SearchAsync(Client, "algebra");

        // Assert
        asBuyer!.TotalCount.Should().Be(2);
        asAnonymous!.TotalCount.Should().Be(2);
        asThird!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_SellerAsksSimilarListings_Then_BuyersListingIsNotSuggested()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _third, _sellersListing, _buyersListing, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        using var anonymous = Factory.CreateClient();
        AuthenticateAs(_seller.Id);

        // Act
        var asSeller = await Client.GetFromJsonAsync<GetSimilarListingsResponse>($"/api/v1/listings/{_sellersListing.Id}/similar", Json);
        var asAnonymous = await anonymous.GetFromJsonAsync<GetSimilarListingsResponse>($"/api/v1/listings/{_sellersListing.Id}/similar", Json);

        // Assert
        asSeller!.Items.Should().BeEmpty();
        asAnonymous!.Items.Select(i => i.Id).Should().Equal(_buyersListing.Id);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_SellerGetsFavorites_Then_BuyersListingIsLeftOutButFavoriteStaysSaved()
    {
        // Arrange
        var favorite = Favorite.Create(_seller.Id, _buyersListing, CreatedAt).Value;
        await SeedAsync(_seller, _buyer, _sellersListing, _buyersListing, favorite, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        AuthenticateAs(_seller.Id);

        // Act
        var result = await Client.GetFromJsonAsync<GetMyFavoritesResponse>("/api/v1/members/me/favorites", Json);

        // Assert
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        (await QueryDbAsync(db => db.Set<Favorite>().CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Given_BlockThroughTheApi_When_BlockingAndUnblocking_Then_AllThreeListsFollow()
    {
        // Arrange
        var favorite = Favorite.Create(_seller.Id, _buyersListing, CreatedAt).Value;
        await SeedAsync(_seller, _buyer, _sellersListing, _buyersListing, favorite);
        AuthenticateAs(_seller.Id);
        (await SearchAsync(Client, "algebra"))!.TotalCount.Should().Be(2);

        // Act + Assert: blocked
        (await Client.PostAsync($"/api/v1/members/{_buyer.Id}/block", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SearchAsync(Client, "algebra"))!.Items.Select(i => i.Id).Should().Equal(_sellersListing.Id);
        (await Client.GetFromJsonAsync<GetSimilarListingsResponse>($"/api/v1/listings/{_sellersListing.Id}/similar", Json))!.Items.Should().BeEmpty();
        (await Client.GetFromJsonAsync<GetMyFavoritesResponse>("/api/v1/members/me/favorites", Json))!.Items.Should().BeEmpty();

        // Act + Assert: unblocked
        (await Client.DeleteAsync($"/api/v1/members/{_buyer.Id}/block")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SearchAsync(Client, "algebra"))!.TotalCount.Should().Be(2);
        (await Client.GetFromJsonAsync<GetSimilarListingsResponse>($"/api/v1/listings/{_sellersListing.Id}/similar", Json))!.Items.Select(i => i.Id).Should().Equal(_buyersListing.Id);
        (await Client.GetFromJsonAsync<GetMyFavoritesResponse>("/api/v1/members/me/favorites", Json))!.Items.Select(i => i.ListingId).Should().Equal(_buyersListing.Id);
    }

    // The anonymous search is cached for 30 seconds. A row added straight in the database does not clear the
    // cache, so the cached answer proves the cache is there and the block (or unblock) call proves it is evicted.
    [Fact]
    public async Task Given_CachedAnonymousSearch_When_BlockingAndUnblocking_Then_CacheIsEvictedEachTime()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _sellersListing);
        using var anonymous = Factory.CreateClient();
        (await SearchAsync(anonymous, "algebra"))!.TotalCount.Should().Be(1);
        await SeedAsync(_buyersListing);
        (await SearchAsync(anonymous, "algebra"))!.TotalCount.Should().Be(1);
        AuthenticateAs(_seller.Id);

        // Act: blocking evicts the cache
        var block = await Client.PostAsync($"/api/v1/members/{_buyer.Id}/block", content: null);

        // Assert
        block.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SearchAsync(anonymous, "algebra"))!.TotalCount.Should().Be(2);

        // Arrange: cache the answer again, then add another listing straight in the database
        var another = ActiveListing(_buyer.Id, "Notite de algebra");
        await SeedAsync(another);
        (await SearchAsync(anonymous, "algebra"))!.TotalCount.Should().Be(2);

        // Act: unblocking evicts the cache
        var unblock = await Client.DeleteAsync($"/api/v1/members/{_buyer.Id}/block");

        // Assert
        unblock.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SearchAsync(anonymous, "algebra"))!.TotalCount.Should().Be(3);
    }
}
