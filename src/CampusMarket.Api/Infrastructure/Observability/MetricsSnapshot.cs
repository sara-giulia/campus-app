namespace CampusMarket.Api.Infrastructure.Observability;

// Point-in-time view returned to the admin JSON endpoint. Numbers are "since
// start": the aggregator keeps running totals, no rolling window.
public sealed record MetricsSnapshot(
    IReadOnlyList<EndpointMetrics> Endpoints,
    IReadOnlyList<PredictionMetrics> MlPredictions);

// Counts and timings for one route template, with a per-status breakdown.
// AverageMs and P95Ms are null when Requests == 0.
public sealed record EndpointMetrics(
    string Route,
    long Requests,
    long Errors,
    double? AverageMs,
    double? P95Ms,
    IReadOnlyDictionary<string, long> StatusCodes);

// Count and average duration of one ML model's predictions.
public sealed record PredictionMetrics(
    string Model,
    long Count,
    double? AverageMs);
