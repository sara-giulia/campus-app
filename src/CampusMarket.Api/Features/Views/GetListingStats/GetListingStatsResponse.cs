namespace CampusMarket.Api.Features.Views.GetListingStats;

public sealed record ListingStatsCounts(int Views, int Favorites, int Messages, int Offers);

public sealed record ListingStatsDay(DateOnly Date, int Views, int Favorites, int Messages, int Offers);

// Daily has one entry for each of the last Days days (UTC), the oldest first, today last, zeros included.
public sealed record GetListingStatsResponse(
    Guid ListingId, int Days, DateOnly From, DateOnly To, ListingStatsCounts Totals, IReadOnlyList<ListingStatsDay> Daily);
