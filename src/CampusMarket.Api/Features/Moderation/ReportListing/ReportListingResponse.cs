using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.ReportListing;

public sealed record ReportListingResponse(Guid Id, Guid ListingId, DateTimeOffset CreatedAt)
{
    public static ReportListingResponse From(Report report) => new(report.Id, report.ListingId, report.CreatedAt);
}
