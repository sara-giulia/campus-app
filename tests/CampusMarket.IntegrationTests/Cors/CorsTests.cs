using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Cors;

public sealed class CorsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private const string FrontendOrigin = "http://localhost:5173";
    private const string DeployedOrigin = "https://campusmarket.example";

    [Fact]
    public async Task Given_FrontendOrigin_When_SendingPreflight_Then_Returns204WithCorsHeaders()
    {
        // Arrange
        using var request = Preflight("/api/v1/auth/login", FrontendOrigin);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        HeaderValue(response, "Access-Control-Allow-Origin").Should().Be(FrontendOrigin);
        HeaderValue(response, "Access-Control-Allow-Methods").Should().Contain("POST");
        HeaderValue(response, "Access-Control-Allow-Headers").Should().ContainEquivalentOf("authorization");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_OriginNotAllowed_When_SendingPreflight_Then_ReturnsNoAllowOriginHeader()
    {
        // Arrange
        using var request = Preflight("/api/v1/auth/login", "http://evil.example");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_FrontendOrigin_When_CreatingListing_Then_Returns201WithAllowOriginAndExposedLocation()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var body = new CreateListingRequest("Manual POO", Category.Textbook, Condition.Good, 50m);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/listings")
        {
            Content = JsonContent.Create(body, options: Json)
        };
        request.Headers.Add("Origin", FrontendOrigin);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        created!.Title.Should().Be("Manual POO");
        created.SellerId.Should().Be(SellerId);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/listings/{created.Id}");

        HeaderValue(response, "Access-Control-Allow-Origin").Should().Be(FrontendOrigin);
        HeaderValue(response, "Access-Control-Expose-Headers").Should().Contain("Location");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(created.Id);
        saved.SellerId.Should().Be(SellerId);
        saved.Title.Should().Be("Manual POO");
    }

    [Fact]
    public async Task Given_NoToken_When_GettingFavoritesFromFrontend_Then_Returns401ProblemDetailsWithAllowOrigin()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/members/me/favorites");
        request.Headers.Add("Origin", FrontendOrigin);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me/favorites");
        problem.TraceId.Should().NotBeEmpty();

        HeaderValue(response, "Access-Control-Allow-Origin").Should().Be(FrontendOrigin);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_PublishingFromFrontend_Then_Returns500ProblemDetailsWithAllowOrigin()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await using var factory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IUnitOfWork, ThrowingUnitOfWork>()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(SellerId));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/listings/{listing.Id}/publish");
        request.Headers.Add("Origin", FrontendOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.InternalServerError);
        problem.Code.Should().Be("Server.Error");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/publish");
        problem.TraceId.Should().NotBeEmpty();

        HeaderValue(response, "Access-Control-Allow-Origin").Should().Be(FrontendOrigin);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_NoOrigin_When_GettingListings_Then_Returns200WithoutAllowOrigin()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotBeEmpty();
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_OriginFromConfiguration_When_SendingPreflightFromIt_Then_ReturnsThatOrigin()
    {
        // Arrange
        await using var factory = WithAllowedOrigin(DeployedOrigin);
        using var client = factory.CreateClient();
        using var request = Preflight("/api/v1/auth/login", DeployedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        HeaderValue(response, "Access-Control-Allow-Origin").Should().Be(DeployedOrigin);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_OriginFromConfiguration_When_SendingPreflightFromLocalhost_Then_ReturnsNoAllowOrigin()
    {
        // Arrange
        await using var factory = WithAllowedOrigin(DeployedOrigin);
        using var client = factory.CreateClient();
        using var request = Preflight("/api/v1/auth/login", FrontendOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    // A copy of the API where index 0 of Cors:AllowedOrigins is replaced, as Cors__AllowedOrigins__0 does in deployment.
    private WebApplicationFactory<Program> WithAllowedOrigin(string origin) =>
        Factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", origin));

    // The OPTIONS request a browser sends before a POST with a JSON body and a token.
    private static HttpRequestMessage Preflight(string url, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return request;
    }

    private static string? HeaderValue(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
