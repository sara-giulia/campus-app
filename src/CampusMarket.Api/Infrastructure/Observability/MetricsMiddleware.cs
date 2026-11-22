using System.Diagnostics;
using Microsoft.AspNetCore.Routing.Patterns;

namespace CampusMarket.Api.Infrastructure.Observability;

// Times every HTTP request and records (route template, status, duration) in
// the MetricsRegistry. Route template means /api/v1/listings/{id} is one
// bucket, not one per id, so the dictionary stays bounded.
//
// /health* and the metrics endpoint itself are excluded on purpose: they are
// polled constantly and would drown out the real traffic in the aggregates.
// Exceptions are not swallowed — UseExceptionHandler translates them to a 500
// response, and the finally block then records that 500 as an error.
public sealed class MetricsMiddleware(RequestDelegate next, MetricsRegistry registry)
{
    // CA1861: pass through a static readonly field instead of an array literal.
    private static readonly string[] ExcludedPaths =
    [
        "/health",
        "/health/live",
        "/health/ready",
        "/api/v1/admin/metrics"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var route = GetRouteTemplate(context);
            registry.RecordRequest(route, context.Request.Method, context.Response.StatusCode, elapsedMs);
        }
    }

    private static bool IsExcluded(PathString path)
    {
        foreach (var excluded in ExcludedPaths)
        {
            if (path.Equals(excluded, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // The route template comes from routing (RoutePattern.RawText). An
    // unmatched request (routing returned 404 with no endpoint) is grouped
    // into a single bucket so a scanner cannot blow up the dictionary with
    // one bucket per bad path.
    private static string GetRouteTemplate(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint route)
        {
            var template = route.RoutePattern.RawText;
            if (!string.IsNullOrEmpty(template))
                return $"{context.Request.Method} /{template.TrimStart('/')}";
        }
        return $"{context.Request.Method} (unmatched)";
    }
}
