using CampusMarket.Api.Common.Listings;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Listings.PublishListing;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Features.Listings.PublishListing;

public class PublishListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");
    private static readonly IOptions<ListingOptions> ListingOptions = Options.Create(new ListingOptions());

    private static Listing BuildDraftListing()
    {
        return Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    [Fact]
    public async Task Given_DraftListing_When_SellerPublishes_Then_ActivatesListingAndSavesOnce()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Active);
        listing.ExpiresAt.Should().Be(Now.AddDays(30));
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
        repository.UpdateCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DraftListing_When_AdminPublishes_Then_ActivatesListingAndSavesOnce()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var command = new PublishListingCommand(listing.Id, AdminId, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DraftListing_When_NonOwnerNonAdminPublishes_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var command = new PublishListingCommand(listing.Id, OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller or an Admin can publish this listing.");
        listing.Status.Should().Be(ListingStatus.Draft);
        listing.Version.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(0);
        spamDetector.CallCount.Should().Be(0);
        reports.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownId_When_Publishing_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var unknownId = Guid.NewGuid();
        var command = new PublishListingCommand(unknownId, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
        spamDetector.CallCount.Should().Be(0);
        reports.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ActiveListing_When_SellerPublishes_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        listing.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotDraft");
        result.Error.Message.Should().Be("Only a draft listing can be published.");
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
        spamDetector.CallCount.Should().Be(0);
        reports.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_DetectorSaysNotSpam_When_Publishing_Then_ChecksListingOnceAndAddsNoReport()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now, "Second edition").Value;
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        spamDetector.CallCount.Should().Be(1);
        spamDetector.LastTitle.Should().Be("Algebra book");
        spamDetector.LastDescription.Should().Be("Second edition");
        spamDetector.LastPrice.Should().Be(25m);
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DetectorSaysSpam_When_Publishing_Then_PublishesAndAddsOneAutomaticReport()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector { Result = new SpamCheck(true, 0.97) };
        var reports = new FakeReportRepository();
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), new FakeCategoryRepository(), ListingOptions);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        reports.Items.Should().ContainSingle();
        reports.Items[0].IsAutomatic.Should().BeTrue();
        reports.Items[0].ListingId.Should().Be(listing.Id);
        reports.Items[0].Reason.Should().Be(Report.AutomaticReason);
        reports.Items[0].SpamProbability.Should().Be(0.97);
        reports.Items[0].CreatedAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DraftInInactiveCategory_When_SellerPublishes_Then_ReturnsCategoryInactiveAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var spamDetector = new FakeSpamDetector();
        var reports = new FakeReportRepository();
        var categories = new FakeCategoryRepository();
        categories.Deactivate(Category.Textbook);
        var handler = new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now), categories, ListingOptions);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInactive");
        result.Error.Message.Should().Be("The category 'Textbook' is not active, a listing cannot be put in it.");
        listing.Status.Should().Be(ListingStatus.Draft);
        listing.Version.Should().Be(1);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
        spamDetector.CallCount.Should().Be(0);
        reports.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_DraftWhoseCategoryIsNotInRepository_When_SellerPublishes_Then_ActivatesListingAndSavesOnce()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var categories = new FakeCategoryRepository();
        categories.Items.Remove(Category.Textbook);
        var handler = new PublishListingHandler(repository, unitOfWork, new FakeSpamDetector(), new FakeReportRepository(), new FixedTimeProvider(Now), categories, ListingOptions);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }
}
