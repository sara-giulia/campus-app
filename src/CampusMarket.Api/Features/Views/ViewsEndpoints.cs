using CampusMarket.Api.Features.Views.GetListingStats;
using CampusMarket.Api.Features.Views.RecordView;

namespace CampusMarket.Api.Features.Views;

public static class ViewsEndpoints
{
    // Under /listings: POST /listings/{id}/views and GET /listings/{id}/stats.
    public static IEndpointRouteBuilder MapViewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/listings");

        RecordViewEndpoint.Map(group);
        GetListingStatsEndpoint.Map(group);

        return app;
    }
}
