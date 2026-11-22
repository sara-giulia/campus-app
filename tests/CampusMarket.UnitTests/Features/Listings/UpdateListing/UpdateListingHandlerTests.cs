using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.UpdateListing;

public class UpdateListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildListing()
    {
        return Listing.Create(OwnerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    [Fact]
    public async Task Given_ExistingListing_When_HandlingValidCommand_Then_UpdatesEntityAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(listing.Id, listing.SellerId, false, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Title.Should().Be("Linear algebra");
        listing.Condition.Should().Be(Condition.New);
        listing.Price.Amount.Should().Be(30m);
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
        repository.UpdateCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var unknownId = Guid.NewGuid();
        var command = new UpdateListingCommand(unknownId, Guid.NewGuid(), false, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExistingListing_When_HandlingWithInvalidData_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(listing.Id, listing.SellerId, false, " ", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleEmpty");
        result.Error.Message.Should().Be("Title must not be empty.");
        unitOfWork.SaveCount.Should().Be(0);
        listing.Title.Should().Be("Algebra book");
        listing.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingOfAnotherMember_When_NonAdminUpdates_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(listing.Id, OtherMemberId, false, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller or an Admin can change this listing.");
        repository.Items.Should().ContainKey(listing.Id);
        listing.Title.Should().Be("Algebra book");
        listing.Condition.Should().Be(Condition.Good);
        listing.Price.Amount.Should().Be(25m);
        listing.Version.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingOfAnotherMember_When_AdminUpdates_Then_UpdatesEntityAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(listing.Id, AdminId, true, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Title.Should().Be("Linear algebra");
        listing.Condition.Should().Be(Condition.New);
        listing.Price.Amount.Should().Be(30m);
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_CommandWithDescription_When_Handling_Then_PassesDescriptionToListingTrimmed()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(
            listing.Id, listing.SellerId, false,
            "Linear algebra", Category.Textbook, Condition.New, 30m, "  Second edition.  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Description.Should().Be("Second edition.");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_CommandWithoutDescription_When_Handling_Then_DescriptionIsNull()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(
            listing.Id, listing.SellerId, false,
            "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Description.Should().BeNull();
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_Handling_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var description = new string('a', 1001);
        var command = new UpdateListingCommand(
            listing.Id, listing.SellerId, false,
            "Linear algebra", Category.Textbook, Condition.New, 30m, description);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be("Description must have at most 1000 characters.");
        unitOfWork.SaveCount.Should().Be(0);
        listing.Title.Should().Be("Algebra book");
        listing.Description.Should().BeNull();
        listing.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_NonOwnerNonAdminUpdates_Then_ReturnsNotFoundNotForbidden()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var unknownId = Guid.NewGuid();
        var command = new UpdateListingCommand(unknownId, OtherMemberId, false, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InactiveTargetCategory_When_MovingListingIntoIt_Then_ReturnsCategoryInactiveAndDoesNotSave()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var categories = new FakeCategoryRepository();
        categories.Deactivate(Category.Notes);
        var handler = new UpdateListingHandler(repository, unitOfWork, categories);
        var command = new UpdateListingCommand(listing.Id, OwnerId, false, "Linear algebra", Category.Notes, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInactive");
        result.Error.Message.Should().Be("The category 'Notes' is not active, a listing cannot be put in it.");
        listing.Title.Should().Be("Algebra book");
        listing.Category.Should().Be(Category.Textbook);
        listing.Condition.Should().Be(Condition.Good);
        listing.Price.Amount.Should().Be(25m);
        listing.Version.Should().Be(1);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveTargetCategory_When_MovingListingIntoIt_Then_UpdatesCategoryAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var categories = new FakeCategoryRepository();
        categories.Deactivate(Category.Textbook);
        var handler = new UpdateListingHandler(repository, unitOfWork, categories);
        var command = new UpdateListingCommand(listing.Id, OwnerId, false, "Linear algebra", Category.Notes, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Category.Should().Be(Category.Notes);
        listing.Version.Should().Be(2);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingAlreadyInInactiveCategory_When_EditingWithoutChangingCategory_Then_UpdatesAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var categories = new FakeCategoryRepository();
        categories.Deactivate(Category.Textbook);
        var handler = new UpdateListingHandler(repository, unitOfWork, categories);
        var command = new UpdateListingCommand(listing.Id, OwnerId, false, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Title.Should().Be("Linear algebra");
        listing.Category.Should().Be(Category.Textbook);
        listing.Condition.Should().Be(Condition.New);
        listing.Price.Amount.Should().Be(30m);
        listing.Version.Should().Be(2);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UndefinedTargetCategory_When_MovingListingIntoIt_Then_ReturnsCategoryInvalidAndDoesNotSave()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork, new FakeCategoryRepository());
        var command = new UpdateListingCommand(listing.Id, OwnerId, false, "Linear algebra", (Category)99, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInvalid");
        result.Error.Message.Should().Be("Unknown category.");
        listing.Category.Should().Be(Category.Textbook);
        listing.Version.Should().Be(1);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
