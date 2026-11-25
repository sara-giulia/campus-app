using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Admin.PriceFeedback;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Admin;

public sealed class PriceFeedbackTests : IntegrationTestBase
{
    private const string Url = "/api/v1/admin/ml/price-feedback";
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DeletedListingId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_NoToken_When_GettingPriceFeedback_Then_Returns401AndKeepsRecord()
    {
        // Arrange
        var record = SaleRecord.Create(DeletedListingId, Category.Textbook, Condition.Good, 60m, 50m, CreatedAt).Value;
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

    [Fact]
    public async Task Given_Student_When_GettingPriceFeedback_Then_Returns403AndKeepsRecord()
    {
        // Arrange
        var record = SaleRecord.Create(DeletedListingId, Category.Textbook, Condition.Good, 60m, 50m, CreatedAt).Value;
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
    public async Task Given_NoSales_When_AdminGetsPriceFeedback_Then_Returns200WithHeaderOnlyCsv()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().Be("feedback.csv");

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Be(PriceFeedbackQuery.Header + "\n");

        var count = await QueryDbAsync(db => db.Set<SaleRecord>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ConfirmedSale_When_AdminGetsPriceFeedback_Then_ReturnsOneRowForTheSale()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(
            SellerId, "Manual POO", Category.Textbook, Condition.Good, 45m, CreatedAt, "Putin folosit").Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);

        AuthenticateAs(SellerId);
        var confirm = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Be(
            PriceFeedbackQuery.Header + "\n" +
            "\"Manual POO\",\"Putin folosit\",Textbook,Good,45,false,\"feedback\"\n");

        var saved = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        saved.ListingId.Should().Be(listing.Id);
        saved.SoldPrice.Should().Be(45m);
    }

    [Fact]
    public async Task Given_SeededSaleRecord_When_AdminGetsPriceFeedback_Then_RowUsesRecordValuesAndEscapedListingText()
    {
        // Arrange
        var listing = Listing.Create(
            SellerId, "Osciloscop \"Rigol\"", Category.Electronics, Condition.New, 150m, CreatedAt,
            "Merge bine, cu sonde").Value;
        var record = SaleRecord.Create(listing.Id, Category.LabEquipment, Condition.Worn, 200m, 99.5m, CreatedAt).Value;
        await SeedAsync(listing, record);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Be(
            PriceFeedbackQuery.Header + "\n" +
            "\"Osciloscop \"\"Rigol\"\"\",\"Merge bine, cu sonde\",LabEquipment,Worn,99.5,false,\"feedback\"\n");

        var saved = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(record);
    }

    [Fact]
    public async Task Given_RecordOfDeletedListing_When_AdminGetsPriceFeedback_Then_RecordIsNotExported()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        var exported = SaleRecord.Create(listing.Id, Category.Textbook, Condition.Good, 60m, 50m, CreatedAt).Value;
        var orphan = SaleRecord.Create(DeletedListingId, Category.Notes, Condition.Worn, 20m, 15m, CreatedAt.AddHours(1)).Value;
        await SeedAsync(listing, exported, orphan);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Be(
            PriceFeedbackQuery.Header + "\n" +
            "\"Manual POO\",\"\",Textbook,Good,50,false,\"feedback\"\n");

        var saved = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().ToListAsync());
        saved.Should().BeEquivalentTo(new[] { exported, orphan });
    }
}
