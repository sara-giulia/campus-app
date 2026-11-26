using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Views;

namespace CampusMarket.Api.Features.Views.GetListingStats;

public sealed class GetListingStatsHandler(IListingRepository listings, IListingViewRepository views, TimeProvider timeProvider)
    : IQueryHandler<GetListingStatsQuery, GetListingStatsResponse>
{
    public const int DefaultDays = 30;
    public const int MaxDays = 365;

    public async Task<Result<GetListingStatsResponse>> HandleAsync(GetListingStatsQuery query, CancellationToken ct)
    {
        if (query.Days is < 1 or > MaxDays)
            return Result<GetListingStatsResponse>.Failure(
                Error.Validation("Stats.InvalidDays", $"Days must be between 1 and {MaxDays}."));

        var listing = await listings.GetByIdAsync(query.ListingId, ct);
        if (listing is null)
            return Result<GetListingStatsResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{query.ListingId}' was not found."));

        if (listing.SellerId != query.RequesterId && !query.IsAdmin)
            return Result<GetListingStatsResponse>.Failure(
                Error.Forbidden("Listing.NotOwner", "Only the seller or an Admin can see the statistics of this listing."));

        var to = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var from = to.AddDays(1 - query.Days);
        var fromTime = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var viewDays = PerDay(await views.GetViewDaysAsync(listing.Id, from, ct));
        var favoriteDays = PerDay((await views.GetFavoriteTimesAsync(listing.Id, fromTime, ct)).Select(DayOf));
        var messageDays = PerDay((await views.GetMessageTimesAsync(listing.Id, fromTime, ct)).Select(DayOf));
        var offerDays = PerDay((await views.GetOfferTimesAsync(listing.Id, fromTime, ct)).Select(DayOf));

        var daily = Enumerable.Range(0, query.Days)
            .Select(i => from.AddDays(i))
            .Select(d => new ListingStatsDay(d, viewDays.GetValueOrDefault(d), favoriteDays.GetValueOrDefault(d),
                messageDays.GetValueOrDefault(d), offerDays.GetValueOrDefault(d)))
            .ToList();

        var totals = new ListingStatsCounts(
            daily.Sum(d => d.Views), daily.Sum(d => d.Favorites), daily.Sum(d => d.Messages), daily.Sum(d => d.Offers));

        return new GetListingStatsResponse(listing.Id, query.Days, from, to, totals, daily);
    }

    private static DateOnly DayOf(DateTimeOffset time) => DateOnly.FromDateTime(time.UtcDateTime);

    private static Dictionary<DateOnly, int> PerDay(IEnumerable<DateOnly> days) =>
        days.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
}
