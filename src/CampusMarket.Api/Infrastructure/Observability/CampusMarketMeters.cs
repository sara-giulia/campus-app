using System.Diagnostics.Metrics;

namespace CampusMarket.Api.Infrastructure.Observability;

// Named Meter with the HTTP and ML instruments. The in-memory MetricsRegistry
// is the source for the admin JSON endpoint; the Meter is here so a later
// OpenTelemetry exporter can subscribe to the same measurements without
// touching the call sites.
public sealed class CampusMarketMeters : IDisposable
{
    public const string MeterName = "CampusMarket";

    private readonly Meter _meter;

    public Counter<long> HttpRequests { get; }
    public Histogram<double> HttpDuration { get; }
    public Counter<long> MlPredictions { get; }
    public Histogram<double> MlDuration { get; }

    public CampusMarketMeters()
    {
        _meter = new Meter(MeterName);
        HttpRequests = _meter.CreateCounter<long>(
            "campusmarket.http.requests",
            unit: "{request}",
            description: "HTTP requests, by route template and status code.");
        HttpDuration = _meter.CreateHistogram<double>(
            "campusmarket.http.duration",
            unit: "ms",
            description: "HTTP request duration, by route template and status code.");
        MlPredictions = _meter.CreateCounter<long>(
            "campusmarket.ml.predictions",
            unit: "{prediction}",
            description: "ML predictions, by model name.");
        MlDuration = _meter.CreateHistogram<double>(
            "campusmarket.ml.duration",
            unit: "ms",
            description: "ML prediction duration, by model name.");
    }

    public void Dispose() => _meter.Dispose();
}
