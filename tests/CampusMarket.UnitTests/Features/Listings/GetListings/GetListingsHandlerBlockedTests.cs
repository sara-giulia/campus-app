using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetListings;

public class GetListingsHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ViewerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BlockedSellerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherSellerId = new("33333333-3333-3333-3333-333333333333");

    private readonly FakeListingRepository _repository = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly Listing _blockedListing;
    private readonly Listing _otherListing;
    private readonly GetListingsHandler _handler;

    public GetListingsHandlerBlockedTests()
    {
        _blockedListing = Active(BlockedSellerId, "Algebra book");
        _otherListing = Active(OtherSellerId, "Lab coat");
        _repository.Items[_blockedListing.Id] = _blockedListing;
        _repository.Items[_otherListing.Id] = _otherListing;
        _handler = new GetListingsHandler(_repository, new FakeSemanticSearch(), _blocks, new FakeMemberRepository(), new FixedTimeProvider(Now));
    }

    private static Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_ViewerBlockedASeller_When_Handling_Then_LeavesTheirListingsOutOfItemsAndTotal()
    {
        _blocks.Add(MemberBlock.Create(ViewerId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetListingsQuery(ViewerId: ViewerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(_otherListing.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_NoViewer_When_Handling_Then_ReturnsEveryListing()
    {
        _blocks.Add(MemberBlock.Create(ViewerId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetListingsQuery(), CancellationToken.None);

        result.Value.Items.Select(i => i.Id).Should().BeEquivalentTo([_blockedListing.Id, _otherListing.Id]);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_SomeoneElseBlockedTheSeller_When_ViewerSearches_Then_ReturnsEveryListing()
    {
        _blocks.Add(MemberBlock.Create(OtherSellerId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetListingsQuery(ViewerId: ViewerId), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_SellerBlockedTheViewer_When_ViewerSearches_Then_ReturnsEveryListing()
    {
        _blocks.Add(MemberBlock.Create(BlockedSellerId, ViewerId, Now).Value);

        var result = await _handler.HandleAsync(new GetListingsQuery(ViewerId: ViewerId), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
    }
}
