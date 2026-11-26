using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.ReportListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class ReportListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_PublishedListing_When_ReportingListing_Then_Returns201AndSavesReport()
    {
        // Arrange
        var reporter = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(reporter, listing);
        AuthenticateAs(reporter.Id);
        var request = new ReportListingRequest("  Fake listing, the book does not exist  ");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reports", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().BeNull();

        var body = await response.Content.ReadFromJsonAsync<ReportListingResponse>(Json);
        body!.Id.Should().NotBeEmpty();
        body.ListingId.Should().Be(listing.Id);

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.ListingId.Should().Be(listing.Id);
        saved.ReporterId.Should().Be(reporter.Id);
        saved.Reason.Should().Be("Fake listing, the book does not exist");
        saved.SpamProbability.Should().BeNull();
    }

    [Fact]
    public async Task Given_ListingAlreadyReportedByMember_When_ReportingListing_Then_Returns409AndKeepsOneReport()
    {
        // Arrange
        var reporter = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var report = Report.Create(reporter.Id, listing, "Fake listing", CreatedAt.AddHours(1)).Value;
        await SeedAsync(reporter, listing, report);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest("Still fake"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.AlreadyExists");
        error.Type.Should().Be(ErrorType.Conflict);

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task Given_Seller_When_ReportingOwnListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(seller, listing);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest("Fake listing"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.OwnListing");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_ReportingListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var reporter = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(reporter, listing);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest("Fake listing"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.ListingNotAvailable");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_BlankReason_When_ReportingListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var reporter = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(reporter, listing);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest("   "), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.ReasonEmpty");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_ReportingListing_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var reporter = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(reporter, listing);
        AuthenticateAs(reporter.Id);
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{unknownId}/reports", new ReportListingRequest("Fake listing"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_ReportingListing_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest("Fake listing"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_ReportingListing_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var reporter = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(reporter, listing);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest("Fake listing"), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/reports");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(0);
    }
}
