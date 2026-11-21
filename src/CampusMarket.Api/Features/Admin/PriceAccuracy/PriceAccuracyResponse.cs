using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Admin.PriceAccuracy;

// Count 0 → Mae and Mape are null (no records yet, metrics undefined).
public sealed record PriceAccuracyResponse(
    int Count,
    decimal? Mae,
    decimal? Mape,
    IReadOnlyList<PriceAccuracyPerCategory> PerCategory,
    IReadOnlyList<PriceAccuracyRecord> LastRecords);

public sealed record PriceAccuracyPerCategory(
    Category Category,
    int Count,
    decimal Mae);

// Title is not stored on the record (it can drift after the sale), so only
// the stable ids and the two prices the dashboard needs come back here.
public sealed record PriceAccuracyRecord(
    Guid ListingId,
    Category Category,
    Condition Condition,
    decimal SuggestedPrice,
    decimal SoldPrice,
    DateTimeOffset SoldAt);
