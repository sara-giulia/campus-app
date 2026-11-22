using System.Collections.Concurrent;
using System.Diagnostics;

namespace CampusMarket.Api.Infrastructure.Observability;

// Thread-safe, in-memory aggregator for HTTP requests (per route template +
// status code) and ML predictions (per model). Fed by MetricsMiddleware and
// the ML services, snapshotted by the admin endpoint. Every call also feeds
// the CampusMarketMeters instruments so a future OpenTelemetry exporter picks
// up the same data without touching call sites.
public sealed class MetricsRegistry(CampusMarketMeters meters)
{
    private readonly ConcurrentDictionary<string, EndpointBucket> _endpoints = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PredictionBucket> _predictions = new(StringComparer.Ordinal);

    public void RecordRequest(string route, string method, int statusCode, double durationMs)
    {
        var bucket = _endpoints.GetOrAdd(route, static _ => new EndpointBucket());
        bucket.Record(statusCode, durationMs);

        var routeTag = new KeyValuePair<string, object?>("route", route);
        var methodTag = new KeyValuePair<string, object?>("method", method);
        var statusTag = new KeyValuePair<string, object?>("status", statusCode);
        meters.HttpRequests.Add(1, routeTag, methodTag, statusTag);
        meters.HttpDuration.Record(durationMs, routeTag, methodTag, statusTag);
    }

    // Times the ML call, records the elapsed time, and returns the prediction.
    // One call site per ML service; the Func captures the real predict() call.
    public T MeasurePrediction<T>(string model, Func<T> predict)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return predict();
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var bucket = _predictions.GetOrAdd(model, static _ => new PredictionBucket());
            bucket.Record(elapsedMs);

            var modelTag = new KeyValuePair<string, object?>("model", model);
            meters.MlPredictions.Add(1, modelTag);
            meters.MlDuration.Record(elapsedMs, modelTag);
        }
    }

    public MetricsSnapshot Snapshot()
    {
        var endpoints = _endpoints
            .Select(kvp => kvp.Value.Snapshot(kvp.Key))
            .OrderBy(e => e.Route, StringComparer.Ordinal)
            .ToList();

        var predictions = _predictions
            .Select(kvp => kvp.Value.Snapshot(kvp.Key))
            .OrderBy(p => p.Model, StringComparer.Ordinal)
            .ToList();

        return new MetricsSnapshot(endpoints, predictions);
    }
}

internal sealed class EndpointBucket
{
    // Bounded reservoir of recent durations. 1024 samples give a stable p95
    // without unbounded memory growth; once full, the oldest sample is
    // overwritten (FIFO ring).
    private const int ReservoirSize = 1024;

    private long _count;
    private long _errorCount;
    // Sum of durations in microseconds, so Interlocked.Add on the integer
    // avoids the fp precision drift of double addition under contention.
    private long _totalMicros;
    private readonly ConcurrentDictionary<int, long> _statusCounts = new();

    private readonly double[] _reservoir = new double[ReservoirSize];
    private readonly object _reservoirLock = new();
    private long _reservoirIndex;
    private int _reservoirCount;

    public void Record(int statusCode, double durationMs)
    {
        Interlocked.Increment(ref _count);
        if (statusCode >= 500)
            Interlocked.Increment(ref _errorCount);

        var micros = (long)(durationMs * 1000.0);
        Interlocked.Add(ref _totalMicros, micros);

        _statusCounts.AddOrUpdate(statusCode, 1L, static (_, v) => v + 1);

        lock (_reservoirLock)
        {
            var idx = (int)(_reservoirIndex % ReservoirSize);
            _reservoir[idx] = durationMs;
            _reservoirIndex++;
            if (_reservoirCount < ReservoirSize)
                _reservoirCount++;
        }
    }

    public EndpointMetrics Snapshot(string route)
    {
        var count = Interlocked.Read(ref _count);
        var errors = Interlocked.Read(ref _errorCount);
        var totalMicros = Interlocked.Read(ref _totalMicros);

        double? avgMs = count > 0 ? totalMicros / 1000.0 / count : null;
        double? p95Ms = ComputeP95();

        var statusCounts = BuildStatusCounts();

        return new EndpointMetrics(route, count, errors, Round(avgMs), Round(p95Ms), statusCounts);
    }

    private double? ComputeP95()
    {
        double[] samples;
        int n;
        lock (_reservoirLock)
        {
            n = _reservoirCount;
            if (n == 0)
                return null;
            samples = new double[n];
            Array.Copy(_reservoir, 0, samples, 0, n);
        }

        Array.Sort(samples);
        var rank = (int)Math.Ceiling(0.95 * n) - 1;
        if (rank < 0) rank = 0;
        if (rank >= n) rank = n - 1;
        return samples[rank];
    }

    // Status codes serialized as strings so the JSON keys look natural
    // ("200", "404") instead of relying on default int-key behaviour.
    private Dictionary<string, long> BuildStatusCounts()
    {
        var snapshot = new Dictionary<string, long>(_statusCounts.Count);
        foreach (var kvp in _statusCounts.OrderBy(kvp => kvp.Key))
            snapshot[kvp.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = kvp.Value;
        return snapshot;
    }

    private static double? Round(double? value) =>
        value is null ? null : Math.Round(value.Value, 2);
}

internal sealed class PredictionBucket
{
    private long _count;
    private long _totalMicros;

    public void Record(double durationMs)
    {
        Interlocked.Increment(ref _count);
        Interlocked.Add(ref _totalMicros, (long)(durationMs * 1000.0));
    }

    public PredictionMetrics Snapshot(string model)
    {
        var count = Interlocked.Read(ref _count);
        var totalMicros = Interlocked.Read(ref _totalMicros);
        double? avgMs = count > 0 ? totalMicros / 1000.0 / count : null;
        return new PredictionMetrics(
            model,
            count,
            avgMs is null ? null : Math.Round(avgMs.Value, 2));
    }
}
