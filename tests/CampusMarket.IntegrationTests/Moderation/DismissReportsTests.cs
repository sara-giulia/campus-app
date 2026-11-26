using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Moderation.GetReportedListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class DismissReportsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ListingWithPendingReports_When_AdminDismissesReports_Then_Returns204AndRecordsNotSpamExample()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing("Manual POO", "Putin folosit.");
        var other = PublishedListing("Calculator stiintific", null);
        var userReport = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        var automatic = Report.CreateAutomatic(listing.Id, 0.9, CreatedAt.AddHours(2));
        var otherReport = Report.Create(ana.Id, other, "Pret prea mic", CreatedAt.AddHours(3)).Value;
        await SeedAsync(ana, listing, other, userReport, automatic, otherReport);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var reports = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().ToListAsync());
        var dismissed = reports.Where(r => r.ListingId == listing.Id).ToList();
        dismissed.Should().HaveCount(2);
        dismissed.Should().OnlyContain(r => r.Resolution == ReportResolution.Dismissed && r.ResolvedAt != null);
        var untouched = reports.Single(r => r.Id == otherReport.Id);
        untouched.Resolution.Should().Be(ReportResolution.Pending);
        untouched.ResolvedAt.Should().BeNull();

        var example = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        example.ListingId.Should().Be(listing.Id);
        example.Kind.Should().Be(TrainingExampleKind.Spam);
        example.Label.Should().Be(TrainingExample.NotSpamLabel);
        example.Source.Should().Be(TrainingExampleSource.Report);
        example.Title.Should().Be("Manual POO");
        example.Description.Should().Be("Putin folosit.");
        example.CreatedBy.Should().Be(AdminId);
        example.CreatedAt.Should().Be(dismissed[0].ResolvedAt!.Value);
        example.UpdatedAt.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_DismissedReports_When_AdminGetsReportedListings_Then_ListingIsNotReturned()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing("Manual POO", null);
        var report = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);
        AuthenticateAs(AdminId, "Admin");
        var dismiss = await Client.PostAsync(DismissUrl(listing.Id), content: null);
        dismiss.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.GetAsync("/api/v1/moderation/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetReportedListingsResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Resolution.Should().Be(ReportResolution.Dismissed);
        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithoutReports_When_AdminDismissesReports_Then_Returns400AndRecordsNothing()
    {
        // Arrange
        var listing = PublishedListing("Manual POO", null);
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.NoPendingReports");
        error.Type.Should().Be(ErrorType.Validation);

        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(0);
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_OnlyAlreadyDismissedReport_When_AdminDismissesReports_Then_Returns400AndKeepsReport()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing("Manual POO", null);
        var report = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        report.Dismiss(CreatedAt.AddHours(2));
        await SeedAsync(ana, listing, report);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(listing.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.NoPendingReports");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Resolution.Should().Be(ReportResolution.Dismissed);
        saved.ResolvedAt.Should().Be(CreatedAt.AddHours(2));
        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_AdminDismissesReports_Then_Returns404AndKeepsReports()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing("Manual POO", null);
        var report = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(Guid.NewGuid()), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await ShouldBeUnchangedAsync(report.Id);
    }

    [Fact]
    public async Task Given_Student_When_DismissingReports_Then_Returns403AndKeepsReports()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing("Manual POO", null);
        var report = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsync(DismissUrl(listing.Id), content: null);

        // Assert
        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Auth.Forbidden", DismissUrl(listing.Id));
        await ShouldBeUnchangedAsync(report.Id);
    }

    [Fact]
    public async Task Given_NoToken_When_DismissingReports_Then_Returns401AndKeepsReports()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing("Manual POO", null);
        var report = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);

        // Act
        var response = await Client.PostAsync(DismissUrl(listing.Id), content: null);

        // Assert
        await ShouldBeProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized", DismissUrl(listing.Id));
        await ShouldBeUnchangedAsync(report.Id);
    }

    private static string DismissUrl(Guid listingId) => $"/api/v1/moderation/listings/{listingId}/dismiss-reports";

    private static Listing PublishedListing(string title, string? description)
    {
        var listing = Listing.Create(SellerId, title, Category.Textbook, Condition.Good, 50m, CreatedAt, description).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code, string instance)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)status);
        problem.Code.Should().Be(code);
        problem.Instance.Should().Be(instance);
        problem.TraceId.Should().NotBeEmpty();
    }

    private async Task ShouldBeUnchangedAsync(Guid reportId)
    {
        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(reportId);
        saved.Resolution.Should().Be(ReportResolution.Pending);
        saved.ResolvedAt.Should().BeNull();
        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(0);
    }
}
