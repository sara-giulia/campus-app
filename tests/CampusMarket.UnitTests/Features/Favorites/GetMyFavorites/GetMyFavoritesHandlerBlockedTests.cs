using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Favorites.GetMyFavorites;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Favorites.GetMyFavorites;

public class GetMyFavoritesHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BlockedSellerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherSellerId = new("33333333-3333-3333-3333-333333333333");

    private readonly FakeFavoriteRepository _favorites = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly Listing _blockedListing;
    private readonly Listing _otherListing;
    private readonly GetMyFavoritesHandler _handler;

    public GetMyFavoritesHandlerBlockedTests()
    {
        _blockedListing = Active(BlockedSellerId, "Algebra book");
        _otherListing = Active(OtherSellerId, "Lab coat");
        var listings = new FakeListingRepository();
        listings.Items[_blockedListing.Id] = _blockedListing;
        listings.Items[_otherListing.Id] = _otherListing;
        _favorites.Items.Add(Favorite.Create(MemberId, _blockedListing, Now).Value);
        _favorites.Items.Add(Favorite.Create(MemberId, _otherListing, Now.AddMinutes(1)).Value);
        _handler = new GetMyFavoritesHandler(_favorites, listings, _blocks, new FakeMemberRepository(), new FixedTimeProvider(Now));
    }

    private static Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_MemberBlockedASeller_When_Handling_Then_LeavesTheirFavoriteOutButKeepsTheRow()
    {
        _blocks.Add(MemberBlock.Create(MemberId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetMyFavoritesQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(_otherListing.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        _favorites.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_NoBlock_When_Handling_Then_ReturnsBothNewestFirst()
    {
        var result = await _handler.HandleAsync(new GetMyFavoritesQuery(MemberId), CancellationToken.None);

        result.Value.Items.Select(i => i.ListingId).Should().Equal(_otherListing.Id, _blockedListing.Id);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_SellerBlockedTheMember_When_Handling_Then_ReturnsBoth()
    {
        _blocks.Add(MemberBlock.Create(BlockedSellerId, MemberId, Now).Value);

        var result = await _handler.HandleAsync(new GetMyFavoritesQuery(MemberId), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
    }
}
