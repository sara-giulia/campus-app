using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Moderation.HideListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.HideListing;

public class HideListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReporterId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherReporterId = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherAdminId = new("55555555-5555-5555-5555-555555555555");

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildPublishedListing()
    {
        var listing = BuildDraftListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_ActiveListing_When_Handling_Then_HidesListingAndSavesOnce()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, new FakeReportRepository(), new FakeTrainingExampleRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Hidden);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, new FakeReportRepository(), new FakeTrainingExampleRepository(), unitOfWork, new FixedTimeProvider(Now));
        var unknownId = Guid.NewGuid();
        var command = new HideListingCommand(unknownId, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_HiddenListing_When_Handling_Then_ReturnsAlreadyHiddenAndDoesNotSave()
    {
        var listing = BuildPublishedListing();
        listing.Hide();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, new FakeReportRepository(), new FakeTrainingExampleRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.AlreadyHidden");
        result.Error.Message.Should().Be("The listing is already hidden.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_ReturnsCannotHideAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, new FakeReportRepository(), new FakeTrainingExampleRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CannotHide");
        result.Error.Message.Should().Be("Only an active or reserved listing can be hidden.");
        listing.Status.Should().Be(ListingStatus.Draft);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingWithPendingReports_When_Handling_Then_ConfirmsReportsAndAddsOneSpamExample()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        var userReport = Report.Create(ReporterId, listing, "Fake photos", Now.AddHours(-2)).Value;
        var automaticReport = Report.CreateAutomatic(listing.Id, 0.95, Now.AddHours(-1));
        reports.Items.Add(userReport);
        reports.Items.Add(automaticReport);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, reports, examples, unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Hidden);
        userReport.Resolution.Should().Be(ReportResolution.Confirmed);
        userReport.ResolvedAt.Should().Be(Now);
        automaticReport.Resolution.Should().Be(ReportResolution.Confirmed);
        automaticReport.ResolvedAt.Should().Be(Now);
        examples.Items.Should().ContainSingle();
        var example = examples.Items[0];
        example.ListingId.Should().Be(listing.Id);
        example.Kind.Should().Be(TrainingExampleKind.Spam);
        example.Label.Should().Be("spam");
        example.Source.Should().Be(TrainingExampleSource.Report);
        example.Title.Should().Be("Algebra book");
        example.CreatedBy.Should().Be(AdminId);
        example.CreatedAt.Should().Be(Now);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithoutReports_When_Handling_Then_HidesListingWithoutExample()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, new FakeReportRepository(), examples, unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Hidden);
        examples.Items.Should().BeEmpty();
        examples.AddCount.Should().Be(0);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithOnlyResolvedReports_When_Handling_Then_HidesListingWithoutExampleAndKeepsResolutions()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        var dismissed = Report.Create(ReporterId, listing, "Fake photos", Now.AddHours(-2)).Value;
        dismissed.Dismiss(Now.AddHours(-1));
        reports.Items.Add(dismissed);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, reports, examples, unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Hidden);
        dismissed.Resolution.Should().Be(ReportResolution.Dismissed);
        dismissed.ResolvedAt.Should().Be(Now.AddHours(-1));
        examples.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_PendingReportOfAnotherListing_When_Handling_Then_ThatReportStaysPending()
    {
        var listing = BuildPublishedListing();
        var otherListing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        repository.Items[otherListing.Id] = otherListing;
        var reports = new FakeReportRepository();
        var otherReport = Report.Create(ReporterId, otherListing, "Fake photos", Now).Value;
        reports.Items.Add(otherReport);
        var examples = new FakeTrainingExampleRepository();
        var handler = new HideListingHandler(repository, reports, examples, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        otherReport.Resolution.Should().Be(ReportResolution.Pending);
        otherReport.ResolvedAt.Should().BeNull();
        otherListing.Status.Should().Be(ListingStatus.Active);
        examples.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ExistingNotSpamExample_When_HidingWithPendingReports_Then_RelabelsItAsSpamWithoutDuplicate()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(OtherReporterId, listing, "Still fake", Now.AddHours(-1)).Value);
        var examples = new FakeTrainingExampleRepository();
        var existing = TrainingExample.ForSpamDecision(listing, false, OtherAdminId, Now.AddDays(-1)).Value;
        examples.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, reports, examples, unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        examples.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        examples.AddCount.Should().Be(0);
        existing.Label.Should().Be("spam");
        existing.CreatedBy.Should().Be(AdminId);
        existing.CreatedAt.Should().Be(Now.AddDays(-1));
        existing.UpdatedAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ExistingCategoryExample_When_HidingWithPendingReports_Then_AddsSeparateSpamExample()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "Fake photos", Now).Value);
        var examples = new FakeTrainingExampleRepository();
        var categoryExample = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now.AddDays(-1)).Value;
        examples.Items.Add(categoryExample);
        var handler = new HideListingHandler(repository, reports, examples, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        examples.Items.Should().HaveCount(2);
        examples.AddCount.Should().Be(1);
        categoryExample.Label.Should().Be("Textbook");
        categoryExample.UpdatedAt.Should().BeNull();
        examples.Items[1].Kind.Should().Be(TrainingExampleKind.Spam);
        examples.Items[1].Label.Should().Be("spam");
    }

    [Fact]
    public async Task Given_EmptyAdminId_When_HidingWithPendingReports_Then_HidesAndConfirmsWithoutExample()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        var report = Report.Create(ReporterId, listing, "Fake photos", Now).Value;
        reports.Items.Add(report);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, reports, examples, unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, Guid.Empty);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Hidden);
        report.Resolution.Should().Be(ReportResolution.Confirmed);
        examples.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_HiddenListingWithPendingReport_When_Handling_Then_ReportStaysPendingAndNoExample()
    {
        var listing = BuildPublishedListing();
        var report = Report.Create(ReporterId, listing, "Fake photos", Now).Value;
        listing.Hide();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(report);
        var examples = new FakeTrainingExampleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, reports, examples, unitOfWork, new FixedTimeProvider(Now));
        var command = new HideListingCommand(listing.Id, AdminId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.AlreadyHidden");
        result.Error.Message.Should().Be("The listing is already hidden.");
        report.Resolution.Should().Be(ReportResolution.Pending);
        report.ResolvedAt.Should().BeNull();
        examples.Items.Should().BeEmpty();
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
