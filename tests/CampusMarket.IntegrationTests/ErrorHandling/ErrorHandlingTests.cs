using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.ErrorHandling;

public sealed class ErrorHandlingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_UnknownRoute_When_Getting_Then_Returns404ProblemDetails()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/does-not-exist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.NotFound);
        problem.Code.Should().Be("Route.NotFound");
        problem.Instance.Should().Be("/api/v1/does-not-exist");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_RouteWithoutDelete_When_Deleting_Then_Returns405ProblemDetails()
    {
        // Act
        var response = await Client.DeleteAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.MethodNotAllowed);
        problem.Code.Should().Be("Request.MethodNotAllowed");
        problem.Instance.Should().Be("/api/v1/listings");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownCategoryInJson_When_CreatingListing_Then_Returns400ProblemDetailsAndSavesNothing()
    {
        // Arrange
        AuthenticateAs(SellerId);
        var json = """
            {
              "title": "Manual POO",
              "category": "Banana",
              "condition": "Good",
              "price": 50
            }
            """;
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await Client.PostAsync("/api/v1/listings", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Code.Should().Be("Request.Invalid");
        problem.Instance.Should().Be("/api/v1/listings");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ConcurrentEdit_When_PublishingListing_Then_Returns409ProblemDetailsAndKeepsOtherChange()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await using var factory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IUnitOfWork, ConcurrentEditUnitOfWork>()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(SellerId));

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        problem.Code.Should().Be("Concurrency.Conflict");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/publish");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_PublishingListing_Then_Returns500ProblemDetailsWithoutExceptionMessage()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        await using var factory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IUnitOfWork, ThrowingUnitOfWork>()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(SellerId));

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("secret");

        var problem = JsonSerializer.Deserialize<ProblemResponse>(raw, Json);
        problem!.Status.Should().Be((int)HttpStatusCode.InternalServerError);
        problem.Code.Should().Be("Server.Error");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/publish");
        problem.TraceId.Should().NotBeEmpty();

        // ProblemResponse has no Detail field, so detail is read from the raw JSON.
        using var document = JsonDocument.Parse(raw);
        document.RootElement.GetProperty("detail").GetString().Should().Be("An unexpected error occurred.");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnotherStudent_When_PublishingListing_Then_Returns403WithHandlerErrorFormat()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(listing);
    }
}
