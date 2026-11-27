using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetListings;

public class GetListingsHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _suspendedSeller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _otherSeller = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeListingRepository _repository = new();
    private readonly FakeSemanticSearch _semanticSearch = new();
    private readonly FakeMemberRepository _members = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _suspendedListing;
    private readonly Listing _otherListing;
    private readonly GetListingsHandler _handler;

    public GetListingsHandlerSuspendedTests()
    {
        _members.Items[_suspendedSeller.Id] = _suspendedSeller;
        _members.Items[_otherSeller.Id] = _otherSeller;
        _suspendedListing = Active(_suspendedSeller.Id, "Algebra book", Now.AddHours(-1));
        _otherListing = Active(_otherSeller.Id, "Calculus notes", Now.AddHours(-2));
        _suspendedSeller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new GetListingsHandler(_repository, _semanticSearch, new FakeMemberBlockRepository(), _members, _clock);
    }

    private Listing Active(Guid sellerId, string title, DateTimeOffset createdAt)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, createdAt).Value;
        listing.Publish(Now.AddDays(30));
        _repository.Items[listing.Id] = listing;
        return listing;
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_Listing_Then_TheirListingsAreLeftOutAndNotCounted()
    {
        var result = await _handler.HandleAsync(new GetListingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(_otherListing.Id);
        result.Value.TotalCount.Should().Be(1);
        _repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SuspendedSellerMatchedByMeaning_When_Searching_Then_IsLeftOut()
    {
        _semanticSearch.Matches.Add(new SemanticMatch(_suspendedListing.Id, 0.9));
        _semanticSearch.Matches.Add(new SemanticMatch(_otherListing.Id, 0.8));

        var result = await _handler.HandleAsync(new GetListingsQuery(Search: "manual"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(_otherListing.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspendedSellerMatchedByTitle_When_Searching_Then_IsLeftOut()
    {
        var result = await _handler.HandleAsync(new GetListingsQuery(Search: "algebra"), CancellationToken.None);

        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_Listing_Then_TheirListingsComeBack()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new GetListingsQuery(), CancellationToken.None);

        result.Value.Items.Select(i => i.Id).Should().Equal(_suspendedListing.Id, _otherListing.Id);
        result.Value.TotalCount.Should().Be(2);
    }
}
