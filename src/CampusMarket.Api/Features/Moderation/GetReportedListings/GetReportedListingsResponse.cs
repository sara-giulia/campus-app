using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.GetReportedListings;

public sealed record GetReportedListingsResponse(
    IReadOnlyList<ReportedListingItem> Items, int Count, int TotalCount, int Page, int PageSize);

// One row per reported listing. FlaggedAsSpam: the spam model reported it when it was published.
// Reasons: the reasons of all its reports, newest first.
public sealed record ReportedListingItem(
    Guid ListingId,
    string Title,
    Guid SellerId,
    ListingStatus Status,
    int ReportCount,
    bool FlaggedAsSpam,
    double? SpamProbability,
    DateTimeOffset LastReportedAt,
    IReadOnlyList<string> Reasons)
{
    // reports is never empty: an item exists only for a listing that has reports.
    public static ReportedListingItem From(Listing listing, IReadOnlyList<Report> reports)
    {
        var newestFirst = reports.OrderByDescending(r => r.CreatedAt).ToList();
        var automatic = reports.FirstOrDefault(r => r.IsAutomatic);

        return new ReportedListingItem(
            listing.Id,
            listing.Title,
            listing.SellerId,
            listing.Status,
            reports.Count,
            automatic is not null,
            automatic?.SpamProbability,
            newestFirst[0].CreatedAt,
            newestFirst.Select(r => r.Reason).ToList());
    }
}
