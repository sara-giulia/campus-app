using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Moderation.DismissReports;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.DismissReports;

public class DismissReportsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReporterId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherReporterId = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherAdminId = new("55555555-5555-5555-5555-555555555555");

    private static Listing BuildPublishedListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now, "Second edition").Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static DismissReportsHandler BuildHandler(
        FakeListingRepository listings, FakeReportRepository reports,
        FakeTrainingExampleRepository examples, FakeUnitOfWork unitOfWork) =>
        new(listings, reports, examples, unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_ListingWithPendingReports_When_Handling_Then_DismissesReportsAndAddsOneNotSpamExample()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        var userReport = Report.Create(ReporterId, listing, "Fake photos", Now.AddHours(-2)).Value;
        var automaticReport = Report.CreateAutomatic(listing.Id, 0.8, Now.AddHours(-1));
        reports.Items.Add(userReport);
        reports.Items.Add(automaticReport);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, reports, examples, unitOfWork);
        var command = new DismissReportsCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        userReport.Resolution.Should().Be(ReportResolution.Dismissed);
        userReport.ResolvedAt.Should().Be(Now);
        automaticReport.Resolution.Should().Be(ReportResolution.Dismissed);
        automaticReport.ResolvedAt.Should().Be(Now);
        examples.Items.Should().ContainSingle();
        var example = examples.Items[0];
        example.ListingId.Should().Be(listing.Id);
        example.Kind.Should().Be(TrainingExampleKind.Spam);
        example.Label.Should().Be("not spam");
        example.Source.Should().Be(TrainingExampleSource.Report);
        example.Title.Should().Be("Algebra book");
        example.Description.Should().Be("Second edition");
        example.CreatedBy.Should().Be(AdminId);
        example.CreatedAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithPendingReports_When_Handling_Then_ListingIsUnchanged()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "Fake photos", Now).Value);
        var handler = BuildHandler(listings, reports, new FakeTrainingExampleRepository(), new FakeUnitOfWork());
        var command = new DismissReportsCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(2);
        listings.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingAndResolvedReports_When_Handling_Then_OnlyPendingReportsAreDismissed()
    {
        var listing = BuildPublishedListing();
        var otherListing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        listings.Items[otherListing.Id] = otherListing;
        var reports = new FakeReportRepository();
        var confirmed = Report.Create(ReporterId, listing, "Fake photos", Now.AddHours(-3)).Value;
        confirmed.Confirm(Now.AddHours(-2));
        var pending = Report.Create(OtherReporterId, listing, "Scam", Now.AddHours(-1)).Value;
        var otherListingReport = Report.Create(ReporterId, otherListing, "Spam", Now).Value;
        reports.Items.Add(confirmed);
        reports.Items.Add(pending);
        reports.Items.Add(otherListingReport);
        var handler = BuildHandler(listings, reports, new FakeTrainingExampleRepository(), new FakeUnitOfWork());
        var command = new DismissReportsCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pending.Resolution.Should().Be(ReportResolution.Dismissed);
        confirmed.Resolution.Should().Be(ReportResolution.Confirmed);
        confirmed.ResolvedAt.Should().Be(Now.AddHours(-2));
        otherListingReport.Resolution.Should().Be(ReportResolution.Pending);
        otherListingReport.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_ExistingSpamExample_When_Dismissing_Then_RelabelsItAsNotSpamWithoutDuplicate()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "Fake photos", Now).Value);
        var examples = new FakeTrainingExampleRepository();
        var existing = TrainingExample.ForSpamDecision(listing, true, OtherAdminId, Now.AddDays(-1)).Value;
        examples.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, reports, examples, unitOfWork);
        var command = new DismissReportsCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        examples.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        examples.AddCount.Should().Be(0);
        existing.Label.Should().Be("not spam");
        existing.CreatedBy.Should().Be(AdminId);
        existing.CreatedAt.Should().Be(Now.AddDays(-1));
        existing.UpdatedAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_EmptyAdminId_When_Dismissing_Then_DismissesReportsWithoutExample()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        var report = Report.Create(ReporterId, listing, "Fake photos", Now).Value;
        reports.Items.Add(report);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, reports, examples, unitOfWork);
        var command = new DismissReportsCommand(listing.Id, Guid.Empty);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        report.Resolution.Should().Be(ReportResolution.Dismissed);
        examples.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFoundAndSavesNothing()
    {
        var listings = new FakeListingRepository();
        var reports = new FakeReportRepository();
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, reports, examples, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new DismissReportsCommand(unknownId, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        examples.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingWithoutReports_When_Handling_Then_ReturnsNoPendingReportsAndSavesNothing()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, new FakeReportRepository(), examples, unitOfWork);
        var command = new DismissReportsCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.NoPendingReports");
        result.Error.Message.Should().Be("The listing has no pending reports.");
        examples.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingWithOnlyResolvedReports_When_Handling_Then_ReturnsNoPendingReportsAndChangesNothing()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        var confirmed = Report.Create(ReporterId, listing, "Fake photos", Now.AddHours(-2)).Value;
        confirmed.Confirm(Now.AddHours(-1));
        reports.Items.Add(confirmed);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, reports, examples, unitOfWork);
        var command = new DismissReportsCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.NoPendingReports");
        result.Error.Message.Should().Be("The listing has no pending reports.");
        confirmed.Resolution.Should().Be(ReportResolution.Confirmed);
        confirmed.ResolvedAt.Should().Be(Now.AddHours(-1));
        examples.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
