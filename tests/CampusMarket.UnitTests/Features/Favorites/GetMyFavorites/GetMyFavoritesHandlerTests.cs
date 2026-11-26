using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Favorites.GetMyFavorites;

public class GetMyFavoritesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildPublishedListing(string title = "Algebra book")
    {
        var listing = Listing.Create(SellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_NoFavorites_When_Handling_Then_ReturnsEmptyPage()
    {
        var favorites = new FakeFavoriteRepository();
        var listings = new FakeListingRepository();
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
        favorites.Items.Should().BeEmpty();
        listings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwoFavorites_When_Handling_Then_ReturnsNewestFirstWithListingDetails()
    {
        var book = BuildPublishedListing();
        var coat = Listing.Create(SellerId, "Lab coat", Category.LabEquipment, Condition.New, 60m, Now).Value;
        coat.Publish(Now.AddDays(30));
        coat.Reserve(OtherMemberId, Now);
        var listings = new FakeListingRepository();
        listings.Items[book.Id] = book;
        listings.Items[coat.Id] = coat;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, book, Now.AddHours(-2)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, coat, Now.AddHours(-1)).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().Equal(
            new MyFavoriteItem(coat.Id, "Lab coat", Category.LabEquipment, Condition.New, 60m, ListingStatus.Reserved, Now.AddHours(-1)),
            new MyFavoriteItem(book.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, ListingStatus.Active, Now.AddHours(-2)));
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(2);
        favorites.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_TwoFavoritesWithSameCreatedAt_When_Handling_Then_OrdersByListingId()
    {
        var first = BuildPublishedListing("First book");
        var second = BuildPublishedListing("Second book");
        var listings = new FakeListingRepository();
        listings.Items[first.Id] = first;
        listings.Items[second.Id] = second;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, first, Now).Value);
        favorites.Items.Add(Favorite.Create(MemberId, second, Now).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().BeInAscendingOrder(i => i.ListingId);
        favorites.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_FavoriteOfOtherMember_When_Handling_Then_IsNotReturned()
    {
        var mine = BuildPublishedListing("My book");
        var theirs = BuildPublishedListing("Their book");
        var listings = new FakeListingRepository();
        listings.Items[mine.Id] = mine;
        listings.Items[theirs.Id] = theirs;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, mine, Now).Value);
        favorites.Items.Add(Favorite.Create(OtherMemberId, theirs, Now).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(mine.Id);
        result.Value.TotalCount.Should().Be(1);
        favorites.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_FavoriteWhoseListingIsMissing_When_Handling_Then_IsNotReturnedNorCounted()
    {
        var kept = BuildPublishedListing("Kept book");
        var missing = BuildPublishedListing("Missing book");
        var listings = new FakeListingRepository();
        listings.Items[kept.Id] = kept;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, kept, Now.AddHours(-2)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, missing, Now.AddHours(-1)).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(kept.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        favorites.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ThreeFavorites_When_RequestingSecondPageOfTwo_Then_ReturnsOldestOnly()
    {
        var oldest = BuildPublishedListing("Oldest");
        var middle = BuildPublishedListing("Middle");
        var newest = BuildPublishedListing("Newest");
        var listings = new FakeListingRepository();
        listings.Items[oldest.Id] = oldest;
        listings.Items[middle.Id] = middle;
        listings.Items[newest.Id] = newest;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, oldest, Now.AddHours(-3)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, middle, Now.AddHours(-2)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, newest, Now.AddHours(-1)).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId, 2, 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(oldest.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        favorites.Items.Should().HaveCount(3);
        listings.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ThreeFavorites_When_RequestingFirstPageOfOne_Then_ReturnsNewestOnly()
    {
        var oldest = BuildPublishedListing("Oldest");
        var middle = BuildPublishedListing("Middle");
        var newest = BuildPublishedListing("Newest");
        var listings = new FakeListingRepository();
        listings.Items[oldest.Id] = oldest;
        listings.Items[middle.Id] = middle;
        listings.Items[newest.Id] = newest;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, oldest, Now.AddHours(-3)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, middle, Now.AddHours(-2)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, newest, Now.AddHours(-1)).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId, 1, 1);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(newest.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        favorites.Items.Should().HaveCount(3);
        listings.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_MaxPageSize_When_Handling_Then_Succeeds()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, listing, Now).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId, 1, 100);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
        result.Value.Count.Should().Be(1);
        favorites.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_PageZero_When_Handling_Then_ReturnsInvalidPage()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, listing, Now).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId, 0, 20);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorites.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
        favorites.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Given_PageSizeOutOfRange_When_Handling_Then_ReturnsInvalidPageSize(int pageSize)
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, listing, Now).Value);
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId, 1, pageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorites.InvalidPageSize");
        result.Error.Message.Should().Be($"Page size must be between 1 and {GetMyFavoritesQuery.MaxPageSize}.");
        favorites.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_DefaultQuery_When_Handling_Then_UsesPageOneAndPageSizeTwenty()
    {
        var favorites = new FakeFavoriteRepository();
        var listings = new FakeListingRepository();
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        favorites.Items.Should().BeEmpty();
        listings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_FavoriteOfHiddenListing_When_Handling_Then_IsNotReturnedNorCounted()
    {
        var active = BuildPublishedListing("Active book");
        var hidden = BuildPublishedListing("Hidden book");
        var listings = new FakeListingRepository();
        listings.Items[active.Id] = active;
        listings.Items[hidden.Id] = hidden;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, active, Now.AddHours(-2)).Value);
        favorites.Items.Add(Favorite.Create(MemberId, hidden, Now.AddHours(-1)).Value);
        hidden.Hide();
        var handler = new GetMyFavoritesHandler(favorites, listings, new FakeMemberBlockRepository());
        var query = new GetMyFavoritesQuery(MemberId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(active.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        favorites.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }
}
