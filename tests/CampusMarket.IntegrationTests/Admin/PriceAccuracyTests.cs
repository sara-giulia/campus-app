using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Admin.PriceAccuracy;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Admin;

public sealed class PriceAccuracyTests : IntegrationTestBase
{
    private const string Url = "/api/v1/admin/ml/price-accuracy";
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TextbookListingId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ElectronicsListingId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_TwoSaleRecords_When_AdminGetsPriceAccuracy_Then_Returns200WithMetricsAndRecords()
    {
        // Arrange
        var textbook = SaleRecord.Create(TextbookListingId, Category.Textbook, Condition.Good, 100m, 80m, CreatedAt).Value;
        var electronics = SaleRecord.Create(ElectronicsListingId, Category.Electronics, Condition.New, 50m, 50m, CreatedAt.AddHours(1)).Value;
        await SeedAsync(textbook, electronics);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PriceAccuracyResponse>(Json);
        body!.Count.Should().Be(2);
        body.Mae.Should().Be(10m);
        body.Mape.Should().Be(12.5m);
        body.PerCategory.Should().BeEquivalentTo(new[]
        {
            new PriceAccuracyPerCategory(Category.Textbook, 1, 20m),
            new PriceAccuracyPerCategory(Category.Electronics, 1, 0m)
        });
        body.LastRecords.Should().Equal(
            new PriceAccuracyRecord(ElectronicsListingId, Category.Electronics, Condition.New, 50m, 50m, CreatedAt.AddHours(1)),
            new PriceAccuracyRecord(TextbookListingId, Category.Textbook, Condition.Good, 100m, 80m, CreatedAt));

        var saved = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().ToListAsync());
        saved.Should().BeEquivalentTo(new[] { textbook, electronics });
    }

    [Fact]
    public async Task Given_Student_When_GettingPriceAccuracy_Then_Returns403AndKeepsRecord()
    {
        // Arrange
        var record = SaleRecord.Create(TextbookListingId, Category.Textbook, Condition.Good, 100m, 80m, CreatedAt).Value;
        await SeedAsync(record);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Code.Should().Be("Auth.Forbidden");
        problem.Instance.Should().Be(Url);
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(record);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingPriceAccuracy_Then_Returns401AndKeepsRecord()
    {
        // Arrange
        var record = SaleRecord.Create(TextbookListingId, Category.Textbook, Condition.Good, 100m, 80m, CreatedAt).Value;
        await SeedAsync(record);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(Url);
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(record);
    }
}
