using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Views.GetListingStats;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Views.GetListingStats;

public class GetListingStatsHandlerTests
{
    // Today is 10 March 2026 (UTC).
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = new("33333333-3333-3333-3333-333333333333");

    private readonly FakeListingRepository _listings = new();
    private readonly FakeListingViewRepository _views = new();
    private readonly GetListingStatsHandler _handler;
    private readonly Listing _listing;

    public GetListingStatsHandlerTests()
    {
        _listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now.AddDays(-60)).Value;
        _listing.Publish(Now.AddDays(30));
        _listings.Items[_listing.Id] = _listing;
        _handler = new GetListingStatsHandler(_listings, _views, new FixedTimeProvider(Now));
    }

    private Task<Result<GetListingStatsResponse>> Get(Guid requesterId, bool isAdmin, int days, Guid? listingId = null) =>
        _handler.HandleAsync(new GetListingStatsQuery(listingId ?? _listing.Id, requesterId, isAdmin, days), CancellationToken.None);

    private static DateTimeOffset At(int month, int day, int hour = 9) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task Given_DaysOutOfRange_When_Getting_Then_FailsWithInvalidDays(int days)
    {
        var result = await Get(SellerId, false, days);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Stats.InvalidDays");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Days must be between 1 and 365.");
        _views.ViewsFrom.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public async Task Given_DaysAtTheLimits_When_Getting_Then_ReturnsOneEntryPerDay(int days)
    {
        var result = await Get(SellerId, false, days);

        result.IsSuccess.Should().BeTrue();
        result.Value.Days.Should().Be(days);
        result.Value.Daily.Should().HaveCount(days);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Getting_Then_FailsWithListingNotFound()
    {
        var unknown = Guid.NewGuid();

        var result = await Get(SellerId, false, 30, unknown);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Be($"Listing '{unknown}' was not found.");
    }

    [Fact]
    public async Task Given_MemberWhoIsNotTheSeller_When_Getting_Then_FailsWithNotOwner()
    {
        var result = await Get(StrangerId, false, 30);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Message.Should().Be("Only the seller or an Admin can see the statistics of this listing.");
        _views.ViewsFrom.Should().BeNull();
    }

    [Fact]
    public async Task Given_TheSeller_When_Getting_Then_Succeeds()
    {
        var result = await Get(SellerId, false, 30);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(_listing.Id);
    }

    [Fact]
    public async Task Given_AnAdminWhoIsNotTheSeller_When_Getting_Then_Succeeds()
    {
        var result = await Get(AdminId, true, 30);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Given_NoData_When_Getting_Then_AllDaysAreZeroAndInOrder()
    {
        var result = await Get(SellerId, false, 7);

        var stats = result.Value;
        stats.From.Should().Be(new DateOnly(2026, 3, 4));
        stats.To.Should().Be(new DateOnly(2026, 3, 10));
        stats.Daily.Select(d => d.Date).Should().Equal(
            Enumerable.Range(4, 7).Select(day => new DateOnly(2026, 3, day)));
        stats.Daily.Should().OnlyContain(d => d.Views == 0 && d.Favorites == 0 && d.Messages == 0 && d.Offers == 0);
        stats.Totals.Should().Be(new ListingStatsCounts(0, 0, 0, 0));
    }

    [Fact]
    public async Task Given_EventsOnDifferentDays_When_Getting_Then_EachLandsOnItsDayAndTotalsAdd()
    {
        _views.ViewDays.AddRange([new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 8)]);
        _views.FavoriteTimes.Add(At(3, 9));
        _views.MessageTimes.AddRange([At(3, 9, 8), At(3, 9, 20), At(3, 5)]);
        _views.OfferTimes.Add(At(3, 8));

        var result = await Get(SellerId, false, 7);

        var daily = result.Value.Daily.ToDictionary(d => d.Date);
        daily[new DateOnly(2026, 3, 10)].Should().Be(new ListingStatsDay(new DateOnly(2026, 3, 10), 2, 0, 0, 0));
        daily[new DateOnly(2026, 3, 9)].Should().Be(new ListingStatsDay(new DateOnly(2026, 3, 9), 0, 1, 2, 0));
        daily[new DateOnly(2026, 3, 8)].Should().Be(new ListingStatsDay(new DateOnly(2026, 3, 8), 1, 0, 0, 1));
        daily[new DateOnly(2026, 3, 5)].Should().Be(new ListingStatsDay(new DateOnly(2026, 3, 5), 0, 0, 1, 0));
        daily[new DateOnly(2026, 3, 7)].Should().Be(new ListingStatsDay(new DateOnly(2026, 3, 7), 0, 0, 0, 0));
        result.Value.Totals.Should().Be(new ListingStatsCounts(3, 1, 3, 1));
        result.Value.Totals.Views.Should().Be(result.Value.Daily.Sum(d => d.Views));
        result.Value.Totals.Messages.Should().Be(result.Value.Daily.Sum(d => d.Messages));
    }

    [Fact]
    public async Task Given_EventAtAnOffsetThatIsAlreadyTheNextUtcDay_When_Getting_Then_CountsOnTheUtcDay()
    {
        // 23:30 at -02:00 on 8 March is 01:30 UTC on 9 March.
        _views.MessageTimes.Add(new DateTimeOffset(2026, 3, 8, 23, 30, 0, TimeSpan.FromHours(-2)));

        var result = await Get(SellerId, false, 7);

        var daily = result.Value.Daily.ToDictionary(d => d.Date);
        daily[new DateOnly(2026, 3, 9)].Messages.Should().Be(1);
        daily[new DateOnly(2026, 3, 8)].Messages.Should().Be(0);
    }

    [Fact]
    public async Task Given_Days_When_Getting_Then_AsksTheRepositoryFromTheFirstDayAtMidnightUtc()
    {
        await Get(SellerId, false, 30);

        // 30 days ending on 10 March start on 9 February.
        _views.ViewsFrom.Should().Be(new DateOnly(2026, 2, 9));
        var midnight = new DateTimeOffset(2026, 2, 9, 0, 0, 0, TimeSpan.Zero);
        _views.FavoritesFrom.Should().Be(midnight);
        _views.MessagesFrom.Should().Be(midnight);
        _views.OffersFrom.Should().Be(midnight);
    }

    [Fact]
    public async Task Given_OneKindOfEvent_When_Getting_Then_TheOtherCountsStayZero()
    {
        _views.MessageTimes.Add(At(3, 10));

        var result = await Get(SellerId, false, 3);

        result.Value.Totals.Should().Be(new ListingStatsCounts(0, 0, 1, 0));
    }
}
