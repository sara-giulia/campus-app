using CampusMarket.Api.Infrastructure.Observability;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Infrastructure.Observability;

public class MetricsRegistryTests
{
    private const string ListingsRoute = "/api/v1/listings";
    private const string MembersRoute = "/api/v1/members";

    [Fact]
    public void Given_NothingRecorded_When_Snapshotting_Then_ReturnsEmptyLists()
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider());

        var snapshot = registry.Snapshot();

        snapshot.Endpoints.Should().BeEmpty();
        snapshot.MlPredictions.Should().BeEmpty();
    }

    [Fact]
    public void Given_EmptyEndpointBucket_When_Snapshotting_Then_ReturnsZeroCountsAndNullTimings()
    {
        var bucket = new EndpointBucket(EndpointBucket.DefaultReservoirSize);

        var metrics = bucket.Snapshot(ListingsRoute);

        metrics.Route.Should().Be(ListingsRoute);
        metrics.Requests.Should().Be(0);
        metrics.Errors.Should().Be(0);
        metrics.AverageMs.Should().BeNull();
        metrics.P95Ms.Should().BeNull();
        metrics.StatusCodes.Should().BeEmpty();
    }

    [Fact]
    public void Given_EmptyPredictionBucket_When_Snapshotting_Then_ReturnsZeroCountAndNullAverage()
    {
        var bucket = new PredictionBucket();

        var metrics = bucket.Snapshot("price");

        metrics.Model.Should().Be("price");
        metrics.Count.Should().Be(0);
        metrics.AverageMs.Should().BeNull();
    }

    [Fact]
    public void Given_RequestsOnTwoRoutes_When_Snapshotting_Then_CountsPerRouteAndStatus()
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider());
        registry.RecordRequest(MembersRoute, "POST", 201, 5);
        registry.RecordRequest(ListingsRoute, "GET", 200, 10);
        registry.RecordRequest(ListingsRoute, "GET", 404, 10);
        registry.RecordRequest(ListingsRoute, "GET", 200, 10);

        var snapshot = registry.Snapshot();

        snapshot.Endpoints.Select(e => e.Route).Should().Equal(ListingsRoute, MembersRoute);
        snapshot.Endpoints[0].Requests.Should().Be(3);
        snapshot.Endpoints[0].StatusCodes.Should().Equal(new Dictionary<string, long> { ["200"] = 2, ["404"] = 1 });
        snapshot.Endpoints[1].Requests.Should().Be(1);
        snapshot.Endpoints[1].StatusCodes.Should().Equal(new Dictionary<string, long> { ["201"] = 1 });
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public void Given_ServerErrorStatus_When_Recording_Then_CountsAsError(int statusCode)
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider());

        registry.RecordRequest(ListingsRoute, "GET", statusCode, 10);

        registry.Snapshot().Endpoints.Single().Errors.Should().Be(1);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(499)]
    public void Given_StatusBelow500_When_Recording_Then_DoesNotCountAsError(int statusCode)
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider());

        registry.RecordRequest(ListingsRoute, "GET", statusCode, 10);

        var endpoint = registry.Snapshot().Endpoints.Single();
        endpoint.Requests.Should().Be(1);
        endpoint.Errors.Should().Be(0);
    }

    [Fact]
    public void Given_DurationsFrom10To100Ms_When_Snapshotting_Then_ReturnsAverage55AndP95Of100()
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider());
        registry.RecordRequest(ListingsRoute, "GET", 200, 70);
        registry.RecordRequest(ListingsRoute, "GET", 200, 10);
        registry.RecordRequest(ListingsRoute, "GET", 200, 100);
        registry.RecordRequest(ListingsRoute, "GET", 200, 40);
        registry.RecordRequest(ListingsRoute, "GET", 200, 20);
        registry.RecordRequest(ListingsRoute, "GET", 200, 90);
        registry.RecordRequest(ListingsRoute, "GET", 200, 30);
        registry.RecordRequest(ListingsRoute, "GET", 200, 60);
        registry.RecordRequest(ListingsRoute, "GET", 200, 50);
        registry.RecordRequest(ListingsRoute, "GET", 200, 80);

        var endpoint = registry.Snapshot().Endpoints.Single();

        endpoint.Requests.Should().Be(10);
        endpoint.AverageMs.Should().Be(55);
        endpoint.P95Ms.Should().Be(100);
    }

    [Fact]
    public void Given_OneRequest_When_Snapshotting_Then_AverageAndP95AreThatDurationRounded()
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider());
        registry.RecordRequest(ListingsRoute, "GET", 200, 12.345);

        var endpoint = registry.Snapshot().Endpoints.Single();

        endpoint.AverageMs.Should().Be(12.35);
        endpoint.P95Ms.Should().Be(12.35);
    }

    [Fact]
    public void Given_ReservoirOfThreeAndFourRequests_When_Snapshotting_Then_OldestIsReplacedAndP95UsesKeptValues()
    {
        using var meters = new CampusMarketMeters();
        var registry = new MetricsRegistry(meters, new ManualTimeProvider(), reservoirCapacity: 3);
        registry.RecordRequest(ListingsRoute, "GET", 200, 100);
        registry.RecordRequest(ListingsRoute, "GET", 200, 10);
        registry.RecordRequest(ListingsRoute, "GET", 200, 20);
        registry.RecordRequest(ListingsRoute, "GET", 200, 30);

        var endpoint = registry.Snapshot().Endpoints.Single();

        endpoint.Requests.Should().Be(4);
        endpoint.AverageMs.Should().Be(40);
        endpoint.P95Ms.Should().Be(30);
    }

    [Fact]
    public void Given_PredictionsOfTwoModels_When_Snapshotting_Then_CountsPerModelWithAverageDuration()
    {
        using var meters = new CampusMarketMeters();
        var time = new ManualTimeProvider();
        var registry = new MetricsRegistry(meters, time);
        registry.MeasurePrediction("price", () => { time.Advance(TimeSpan.FromMilliseconds(10)); return 100; });
        registry.MeasurePrediction("price", () => { time.Advance(TimeSpan.FromMilliseconds(30)); return 120; });
        registry.MeasurePrediction("category", () => { time.Advance(TimeSpan.FromMilliseconds(5)); return "Notes"; });

        var predictions = registry.Snapshot().MlPredictions;

        predictions.Should().Equal(
            new PredictionMetrics("category", 1, 5),
            new PredictionMetrics("price", 2, 20));
    }

    [Fact]
    public void Given_Prediction_When_Measuring_Then_ReturnsThePredictedValue()
    {
        using var meters = new CampusMarketMeters();
        var time = new ManualTimeProvider();
        var registry = new MetricsRegistry(meters, time);

        var result = registry.MeasurePrediction("price", () => { time.Advance(TimeSpan.FromMilliseconds(8)); return 42; });

        result.Should().Be(42);
        registry.Snapshot().MlPredictions.Should().Equal(new PredictionMetrics("price", 1, 8));
    }

    [Fact]
    public void Given_PredictionThatThrows_When_Measuring_Then_StillRecordsItsDuration()
    {
        using var meters = new CampusMarketMeters();
        var time = new ManualTimeProvider();
        var registry = new MetricsRegistry(meters, time);

        Action act = () => registry.MeasurePrediction<int>("spam", () => { time.Advance(TimeSpan.FromMilliseconds(3)); throw new InvalidOperationException("model failed"); });

        act.Should().Throw<InvalidOperationException>().WithMessage("model failed");
        registry.Snapshot().MlPredictions.Should().Equal(new PredictionMetrics("spam", 1, 3));
    }
}
