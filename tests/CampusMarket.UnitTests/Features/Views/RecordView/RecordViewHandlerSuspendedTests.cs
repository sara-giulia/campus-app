using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Views.RecordView;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Views.RecordView;

public class RecordViewHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid VisitorId = new("33333333-3333-3333-3333-333333333333");

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly FakeListingViewRepository _views = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _listing;
    private readonly RecordViewHandler _handler;

    public RecordViewHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_seller.Id] = _seller;
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new RecordViewHandler(listings, _views, members, _clock);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_RecordingView_Then_ReturnsFalseAndAddsNothing()
    {
        var result = await _handler.HandleAsync(new RecordViewCommand(_listing.Id, null, VisitorId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        _views.TryAddCount.Should().Be(0);
        _views.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SuspensionOver_When_RecordingView_Then_CountsTheView()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new RecordViewCommand(_listing.Id, null, VisitorId), CancellationToken.None);

        result.Value.Should().BeTrue();
        _views.Items.Should().ContainSingle();
    }
}
