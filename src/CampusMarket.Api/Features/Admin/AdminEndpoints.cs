using CampusMarket.Api.Features.Admin.PriceAccuracy;
using CampusMarket.Api.Features.Admin.PriceFeedback;

namespace CampusMarket.Api.Features.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Admins only: /api/v1/admin/ml
        var ml = app.MapGroup("/admin/ml");
        PriceAccuracyEndpoint.Map(ml);
        PriceFeedbackEndpoint.Map(ml);

        return app;
    }
}
