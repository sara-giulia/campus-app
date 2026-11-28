using CampusMarket.Api.Features.Moderation.DismissMemberReports;
using CampusMarket.Api.Features.Moderation.DismissReports;
using CampusMarket.Api.Features.Moderation.GetReportedMembers;
using CampusMarket.Api.Features.Moderation.GetReportedListings;
using CampusMarket.Api.Features.Moderation.HideListing;
using CampusMarket.Api.Features.Moderation.LiftSuspension;
using CampusMarket.Api.Features.Moderation.ReportListing;
using CampusMarket.Api.Features.Moderation.ReportMember;
using CampusMarket.Api.Features.Moderation.SuspendMember;

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

        // Any logged-in member reports a member: /api/v1/members/{id}/reports
        ReportMemberEndpoint.Map(app.MapGroup("/members"));

        // Admins only: /api/v1/moderation/members
        var members = app.MapGroup("/moderation/members");
        GetReportedMembersEndpoint.Map(members);
        DismissMemberReportsEndpoint.Map(members);
        SuspendMemberEndpoint.Map(members);
        LiftSuspensionEndpoint.Map(members);

        return app;
    }
}
