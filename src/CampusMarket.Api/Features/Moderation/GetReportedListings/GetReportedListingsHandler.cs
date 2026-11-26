using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.GetReportedListings;

public sealed class GetReportedListingsHandler(
    IReportRepository reports,
    IListingRepository listings)
    : IQueryHandler<GetReportedListingsQuery, GetReportedListingsResponse>
{
    public async Task<Result<GetReportedListingsResponse>> HandleAsync(
        GetReportedListingsQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<GetReportedListingsResponse>.Failure(
                Error.Validation("Moderation.InvalidPage", "Page must be at least 1."));
        if (query.PageSize < 1 || query.PageSize > GetReportedListingsQuery.MaxPageSize)
            return Result<GetReportedListingsResponse>.Failure(
                Error.Validation("Moderation.InvalidPageSize",
                    $"Page size must be between 1 and {GetReportedListingsQuery.MaxPageSize}."));

        var allReports = await reports.GetAllAsync(ct);
        var allListings = await listings.GetAllAsync(ct);
        var listingsById = allListings.ToDictionary(l => l.Id);

        // Only pending reports: a confirmed or dismissed report has already been handled by an Admin.
        // A hidden listing has already been handled and an expired one is not public, so both leave the list.
        // Deleting a listing deletes its reports (cascade), so every report has its listing.
        var ordered = allReports
            .Where(r => r.IsPending)
            .GroupBy(r => r.ListingId)
            .Where(g => listingsById.TryGetValue(g.Key, out var listing)
                        && listing.Status is not (ListingStatus.Hidden or ListingStatus.Expired))
            .Select(g => ReportedListingItem.From(listingsById[g.Key], g.ToList()))
            .OrderByDescending(i => i.ReportCount)
            .ThenByDescending(i => i.LastReportedAt)
            .ThenBy(i => i.ListingId)
            .ToList();

        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new GetReportedListingsResponse(items, items.Count, ordered.Count, query.Page, query.PageSize);
    }
}
