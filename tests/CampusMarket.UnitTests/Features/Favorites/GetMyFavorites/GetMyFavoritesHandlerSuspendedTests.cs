using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Favorites.GetMyFavorites;

public class GetMyFavoritesHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");

    private readonly Member _suspendedSeller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _otherSeller = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeFavoriteRepository _favorites = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _suspendedListing;
    private readonly Listing _otherListing;
    private readonly GetMyFavoritesHandler _handler;

    public GetMyFavoritesHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_suspendedSeller.Id] = _suspendedSeller;
        members.Items[_otherSeller.Id] = _otherSeller;
        _suspendedListing = Active(_suspendedSeller.Id, "Algebra book");
        _otherListing = Active(_otherSeller.Id, "Lab coat");
        var listings = new FakeListingRepository();
        listings.Items[_suspendedListing.Id] = _suspendedListing;
        listings.Items[_otherListing.Id] = _otherListing;
        _favorites.Items.Add(Favorite.Create(MemberId, _suspendedListing, Now.AddMinutes(1)).Value);
        _favorites.Items.Add(Favorite.Create(MemberId, _otherListing, Now).Value);
        _suspendedSeller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new GetMyFavoritesHandler(_favorites, listings, new FakeMemberBlockRepository(), members, _clock);
    }

    private static Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_FavoriteOfSuspendedSeller_When_Handling_Then_LeavesItOutButKeepsTheRow()
    {
        var result = await _handler.HandleAsync(new GetMyFavoritesQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(_otherListing.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        _favorites.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_Handling_Then_TheFavoriteComesBack()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new GetMyFavoritesQuery(MemberId), CancellationToken.None);

        result.Value.Items.Select(i => i.ListingId).Should().Equal(_suspendedListing.Id, _otherListing.Id);
        result.Value.TotalCount.Should().Be(2);
    }
}
