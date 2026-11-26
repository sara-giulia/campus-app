using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Views.RecordView;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Views.RecordView;

public class RecordViewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VisitorId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherVisitorId = new("44444444-4444-4444-4444-444444444444");

    private readonly FakeListingRepository _listings = new();
    private readonly FakeListingViewRepository _views = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly RecordViewHandler _handler;

    public RecordViewHandlerTests()
    {
        _handler = new RecordViewHandler(_listings, _views, _clock);
    }

    private Listing Seed(ListingStatus status)
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        if (status != ListingStatus.Draft)
            listing.Publish(Now.AddDays(30));

        switch (status)
        {
            case ListingStatus.Reserved:
                listing.Reserve(BuyerId, Now);
                break;
            case ListingStatus.Sold:
                var reservation = listing.Reserve(BuyerId, Now).Value;
                listing.ConfirmSale(reservation.Id, Now);
                break;
            case ListingStatus.Hidden:
                listing.Hide();
                break;
            case ListingStatus.Expired:
                listing.Expire(Now.AddDays(31));
                break;
        }

        listing.Status.Should().Be(status);
        _listings.Items[listing.Id] = listing;
        return listing;
    }

    private Task<Result<bool>> Record(Guid listingId, Guid? memberId, Guid? visitorId) =>
        _handler.HandleAsync(new RecordViewCommand(listingId, memberId, visitorId), CancellationToken.None);

    [Fact]
    public async Task Given_VisitorOnActiveListing_When_Recording_Then_AddsViewWithVisitorKey()
    {
        var listing = Seed(ListingStatus.Active);

        var result = await Record(listing.Id, null, VisitorId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        var view = _views.Items.Should().ContainSingle().Subject;
        view.ListingId.Should().Be(listing.Id);
        view.ViewerKey.Should().Be($"v:{VisitorId:N}");
        view.Day.Should().Be(new DateOnly(2026, 1, 1));
        view.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Given_LoggedInMember_When_Recording_Then_AddsViewWithMemberKey()
    {
        var listing = Seed(ListingStatus.Active);

        var result = await Record(listing.Id, BuyerId, null);

        result.Value.Should().BeTrue();
        _views.Items.Should().ContainSingle().Which.ViewerKey.Should().Be($"m:{BuyerId:N}");
    }

    [Fact]
    public async Task Given_SameViewerTwiceOnTheSameDay_When_Recording_Then_SecondIsNotCounted()
    {
        var listing = Seed(ListingStatus.Active);

        var first = await Record(listing.Id, null, VisitorId);
        _clock.Now = Now.AddHours(5);
        var second = await Record(listing.Id, null, VisitorId);

        first.Value.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().BeFalse();
        _views.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_SameViewerOnTheNextDay_When_Recording_Then_IsCountedAgain()
    {
        var listing = Seed(ListingStatus.Active);

        await Record(listing.Id, null, VisitorId);
        _clock.Now = Now.AddDays(1);
        var second = await Record(listing.Id, null, VisitorId);

        second.Value.Should().BeTrue();
        _views.Items.Should().HaveCount(2);
        _views.Items.Select(v => v.Day).Should().BeEquivalentTo([new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)]);
    }

    [Fact]
    public async Task Given_TwoDifferentViewers_When_Recording_Then_BothCount()
    {
        var listing = Seed(ListingStatus.Active);

        var first = await Record(listing.Id, null, VisitorId);
        var second = await Record(listing.Id, null, OtherVisitorId);

        first.Value.Should().BeTrue();
        second.Value.Should().BeTrue();
        _views.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SameVisitorOnTwoListings_When_Recording_Then_BothCount()
    {
        var first = Seed(ListingStatus.Active);
        var second = Seed(ListingStatus.Active);

        await Record(first.Id, null, VisitorId);
        var result = await Record(second.Id, null, VisitorId);

        result.Value.Should().BeTrue();
        _views.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_TokenAndVisitorId_When_Recording_Then_UsesTheMemberKey()
    {
        var listing = Seed(ListingStatus.Active);

        await Record(listing.Id, BuyerId, VisitorId);

        _views.Items.Should().ContainSingle().Which.ViewerKey.Should().Be($"m:{BuyerId:N}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_TheSeller_When_Recording_Then_IsNotCounted(bool withVisitorId)
    {
        var listing = Seed(ListingStatus.Active);

        var result = await Record(listing.Id, SellerId, withVisitorId ? VisitorId : null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        _views.TryAddCount.Should().Be(0);
        _views.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Hidden)]
    public async Task Given_ListingThatIsNotPublic_When_Recording_Then_IsNotCounted(ListingStatus status)
    {
        var listing = Seed(status);

        var result = await Record(listing.Id, null, VisitorId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        _views.TryAddCount.Should().Be(0);
        _views.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Expired)]
    public async Task Given_ReservedSoldOrExpiredListing_When_Recording_Then_IsCounted(ListingStatus status)
    {
        var listing = Seed(status);

        var result = await Record(listing.Id, null, VisitorId);

        result.Value.Should().BeTrue();
        _views.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_NoMemberAndNoVisitor_When_Recording_Then_FailsWithViewerMissing()
    {
        var listing = Seed(ListingStatus.Active);

        var result = await Record(listing.Id, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("View.ViewerMissing");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Log in or send a valid visitor id in the X-Visitor-Id header.");
        _views.TryAddCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Recording_Then_FailsWithListingNotFound()
    {
        var unknown = Guid.NewGuid();

        var result = await Record(unknown, null, VisitorId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Be($"Listing '{unknown}' was not found.");
        _views.TryAddCount.Should().Be(0);
    }
}
