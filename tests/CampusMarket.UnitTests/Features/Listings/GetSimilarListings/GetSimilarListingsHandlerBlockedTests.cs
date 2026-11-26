using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetSimilarListings;

public class GetSimilarListingsHandlerBlockedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ViewerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BlockedSellerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherSellerId = new("33333333-3333-3333-3333-333333333333");

    private readonly FakeListingRepository _repository = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeSimilarListingsFinder _finder = new();
    private readonly Listing _target;
    private readonly Listing _blockedCandidate;
    private readonly Listing _otherCandidate;
    private readonly GetSimilarListingsHandler _handler;

    public GetSimilarListingsHandlerBlockedTests()
    {
        _target = Active(OtherSellerId, "Algebra book");
        _blockedCandidate = Active(BlockedSellerId, "Algebra workbook");
        _otherCandidate = Active(OtherSellerId, "Calculus book");
        foreach (var listing in new[] { _target, _blockedCandidate, _otherCandidate })
            _repository.Items[listing.Id] = listing;
        // The blocked seller's listing is the closest one.
        _finder.SimilarityByTitle["Algebra workbook"] = 0.9;
        _finder.SimilarityByTitle["Calculus book"] = 0.4;
        _handler = new GetSimilarListingsHandler(_repository, _finder, _blocks);
    }

    private static Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_ViewerBlockedASeller_When_Handling_Then_TheirListingIsNeverACandidate()
    {
        _blocks.Add(MemberBlock.Create(ViewerId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetSimilarListingsQuery(_target.Id, ViewerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(_otherCandidate.Id);
        _finder.LastCandidates.Select(c => c.Title).Should().Equal("Calculus book");
    }

    [Fact]
    public async Task Given_NoViewer_When_Handling_Then_ClosestListingComesFirst()
    {
        _blocks.Add(MemberBlock.Create(ViewerId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetSimilarListingsQuery(_target.Id), CancellationToken.None);

        result.Value.Items.Select(i => i.Id).Should().Equal(_blockedCandidate.Id, _otherCandidate.Id);
    }

    [Fact]
    public async Task Given_SomeoneElseBlockedTheSeller_When_ViewerAsks_Then_ClosestListingComesFirst()
    {
        _blocks.Add(MemberBlock.Create(OtherSellerId, BlockedSellerId, Now).Value);

        var result = await _handler.HandleAsync(new GetSimilarListingsQuery(_target.Id, ViewerId), CancellationToken.None);

        result.Value.Items.Select(i => i.Id).Should().Equal(_blockedCandidate.Id, _otherCandidate.Id);
    }
}
