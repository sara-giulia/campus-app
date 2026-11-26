using CampusMarket.Api.Features.Moderation.DismissReports;
using CampusMarket.Api.Features.Moderation.GetReportedListings;
using CampusMarket.Api.Features.Moderation.HideListing;
using CampusMarket.Api.Features.Moderation.ReportListing;

namespace CampusMarket.Api.Features.Moderation;

public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        // Any logged-in member reports a listing: /api/v1/listings/{listingId}/reports
        ReportListingEndpoint.Map(app.MapGroup("/listings"));

        // Admins only: /api/v1/moderation/listings
        var moderation = app.MapGroup("/moderation/listings");
        GetReportedListingsEndpoint.Map(moderation);
        HideListingEndpoint.Map(moderation);
        DismissReportsEndpoint.Map(moderation);

        return app;
    }
}
