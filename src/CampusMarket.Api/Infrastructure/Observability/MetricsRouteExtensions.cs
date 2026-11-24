using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Infrastructure.Observability;

// GET /api/v1/admin/metrics. Admin only; without a token the response is
// 401, with a non-admin token it is 403. Returns a point-in-time snapshot of
// the in-memory registry — no database, never fails.
//
// Lives in Infrastructure (next to the registry it reads) instead of under
// Features/, because the architecture tests forbid any Features type from
// depending on Infrastructure, and the registry itself is cross-cutting. The
// mapping follows the same pattern as MapHealthChecks: wired from Program.cs.
public static class MetricsRouteExtensions
{
    public static IEndpointRouteBuilder MapAdminMetrics(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/metrics", Handle)
            .WithName("GetAdminMetrics")
            .RequireAuthorization("Admin");
        return app;
    }

    private static Ok<MetricsSnapshot> Handle(MetricsRegistry registry) =>
        TypedResults.Ok(registry.Snapshot());
}
