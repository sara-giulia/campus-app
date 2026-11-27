using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetSimilarListings;

public class GetSimilarListingsHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _suspendedSeller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _otherSeller = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeListingRepository _repository = new();
    private readonly FakeSimilarListingsFinder _finder = new();
    private readonly FakeMemberRepository _members = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _target;
    private readonly Listing _suspendedCandidate;
    private readonly Listing _otherCandidate;
    private readonly Listing _suspendedTarget;
    private readonly GetSimilarListingsHandler _handler;

    public GetSimilarListingsHandlerSuspendedTests()
    {
        _members.Items[_suspendedSeller.Id] = _suspendedSeller;
        _members.Items[_otherSeller.Id] = _otherSeller;
        _target = Active(_otherSeller.Id, "Algebra book");
        _suspendedCandidate = Active(_suspendedSeller.Id, "Algebra workbook");
        _otherCandidate = Active(_otherSeller.Id, "Calculus book");
        _suspendedTarget = Active(_suspendedSeller.Id, "Physics notes");
        // The suspended seller's listing is the closest one.
        _finder.SimilarityByTitle["Algebra workbook"] = 0.9;
        _finder.SimilarityByTitle["Calculus book"] = 0.4;
        _suspendedSeller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new GetSimilarListingsHandler(_repository, _finder, new FakeMemberBlockRepository(), _members, _clock);
    }

    private Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        _repository.Items[listing.Id] = listing;
        return listing;
    }

    [Fact]
    public async Task Given_SuspendedSellersListingIsClosest_When_Handling_Then_ItIsNeverACandidate()
    {
        var result = await _handler.HandleAsync(new GetSimilarListingsQuery(_target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(_otherCandidate.Id);
        _finder.LastCandidates.Select(c => c.Title).Should().Equal("Calculus book");
    }

    [Fact]
    public async Task Given_TargetOfSuspendedSeller_When_Handling_Then_ReturnsListingNotFoundAndDoesNotAskTheFinder()
    {
        var result = await _handler.HandleAsync(new GetSimilarListingsQuery(_suspendedTarget.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{_suspendedTarget.Id}' was not found.");
        _finder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_Handling_Then_TheirListingIsACandidateAgain()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new GetSimilarListingsQuery(_target.Id), CancellationToken.None);

        result.Value.Items.Select(i => i.Id).Should().StartWith(_suspendedCandidate.Id);
        _repository.Items.Should().HaveCount(4);
    }
}
