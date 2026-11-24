using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.SuggestPrice;
using CampusMarket.Api.Infrastructure.Observability;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Admin;

public sealed class MetricsTests : IntegrationTestBase
{
    private const string Url = "/api/v1/admin/metrics";
    private const string CategoriesRoute = "GET /api/v1/categories/";
    private const string ListingByIdRoute = "GET /api/v1/listings/{id:guid}";
    private const string UnmatchedRoute = "GET (unmatched)";
    private const string PublishRoute = "POST /api/v1/listings/{id:guid}/publish";

    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_NoToken_When_GettingMetrics_Then_Returns401()
    {
        // Arrange: no AuthenticateAs

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_Student_When_GettingMetrics_Then_Returns403()
    {
        // Arrange
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.Forbidden");
        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_OneRequest_When_AdminGetsMetrics_Then_Returns200WithExpectedShape()
    {
        // Arrange
        (await Client.GetAsync("/api/v1/categories")).StatusCode.Should().Be(HttpStatusCode.OK);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("endpoints").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("mlPredictions").ValueKind.Should().Be(JsonValueKind.Array);

        var endpoint = root.GetProperty("endpoints").EnumerateArray().Single();
        endpoint.GetProperty("route").ValueKind.Should().Be(JsonValueKind.String);
        endpoint.GetProperty("requests").ValueKind.Should().Be(JsonValueKind.Number);
        endpoint.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Number);
        endpoint.GetProperty("averageMs").ValueKind.Should().Be(JsonValueKind.Number);
        endpoint.GetProperty("p95Ms").ValueKind.Should().Be(JsonValueKind.Number);
        endpoint.GetProperty("statusCodes").ValueKind.Should().Be(JsonValueKind.Object);

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_TwoCategoryRequestsAndOne404_When_AdminGetsMetrics_Then_CountsPerRouteAndStatusWithoutErrors()
    {
        // Arrange
        (await Client.GetAsync("/api/v1/categories")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/api/v1/categories")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MetricsSnapshot>(Json);
        body!.Endpoints.Select(e => e.Route).Should().BeEquivalentTo(CategoriesRoute, ListingByIdRoute);

        var categories = body.Endpoints.Single(e => e.Route == CategoriesRoute);
        categories.Requests.Should().Be(2);
        categories.Errors.Should().Be(0);
        categories.StatusCodes.Should().BeEquivalentTo(new Dictionary<string, long> { ["200"] = 2 });
        categories.AverageMs.Should().NotBeNull();
        categories.P95Ms.Should().NotBeNull();

        var listingById = body.Endpoints.Single(e => e.Route == ListingByIdRoute);
        listingById.Requests.Should().Be(1);
        listingById.Errors.Should().Be(0);
        listingById.StatusCodes.Should().BeEquivalentTo(new Dictionary<string, long> { ["404"] = 1 });

        body.MlPredictions.Should().BeEmpty();

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_UnknownRoute_When_AdminGetsMetrics_Then_CountsItInTheUnmatchedBucket()
    {
        // Arrange
        (await Client.GetAsync("/api/v1/does-not-exist")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MetricsSnapshot>(Json);
        var unmatched = body!.Endpoints.Should().ContainSingle().Subject;
        unmatched.Route.Should().Be(UnmatchedRoute);
        unmatched.Requests.Should().Be(1);
        unmatched.Errors.Should().Be(0);
        unmatched.StatusCodes.Should().BeEquivalentTo(new Dictionary<string, long> { ["404"] = 1 });

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_HealthAndMetricsRequests_When_AdminGetsMetrics_Then_NothingIsCounted()
    {
        // Arrange
        (await Client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
        AuthenticateAs(AdminId, "Admin");
        (await Client.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MetricsSnapshot>(Json);
        body!.Endpoints.Should().BeEmpty();
        body.MlPredictions.Should().BeEmpty();

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_RequestThatFailsWith500_When_AdminGetsMetrics_Then_CountsItAsError()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await using var factory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IUnitOfWork, ThrowingUnitOfWork>()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(SellerId));
        (await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null))
            .StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(AdminId, "Admin"));

        // Act
        var response = await client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MetricsSnapshot>(Json);
        // The 500 is counted on its own route, not on "POST (unmatched)".
        var publish = body!.Endpoints.Should().ContainSingle().Subject;
        publish.Route.Should().Be(PublishRoute);
        publish.Requests.Should().Be(1);
        publish.Errors.Should().Be(1);
        publish.StatusCodes.Should().BeEquivalentTo(new Dictionary<string, long> { ["500"] = 1 });

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_OnePriceSuggestion_When_AdminGetsMetrics_Then_PriceModelHasOnePrediction()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var request = new SuggestPriceRequest(
            Title: "Laptop Lenovo ThinkPad T480, i5, 16GB RAM",
            Category: Category.Electronics,
            Condition: Condition.New);
        (await Client.PostAsJsonAsync("/api/v1/listings/price-suggestion", request, Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MetricsSnapshot>(Json);
        var price = body!.MlPredictions.Should().ContainSingle().Subject;
        price.Model.Should().Be("price");
        price.Count.Should().Be(1);
        price.AverageMs.Should().NotBeNull();

        await AssertDatabaseUnchangedAsync();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)status);
        problem.Code.Should().Be(code);
        problem.Instance.Should().Be(Url);
        problem.TraceId.Should().NotBeEmpty();
    }

    // The metrics endpoint never touches the database: still no listing and the 5 seeded categories.
    private async Task AssertDatabaseUnchangedAsync()
    {
        var listings = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listings.Should().Be(0);

        var categories = await QueryDbAsync(db => db.Set<ListingCategory>().CountAsync(c => c.IsActive));
        categories.Should().Be(5);
    }
}
