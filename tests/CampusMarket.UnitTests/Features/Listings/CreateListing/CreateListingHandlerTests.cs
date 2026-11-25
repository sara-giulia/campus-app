using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.CreateListing;

public class CreateListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidCommand_When_Handling_Then_StoresListingAndSavesOnce()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var sellerId = Guid.NewGuid();
        var command = new CreateListingCommand(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SellerId.Should().Be(sellerId);
        result.Value.Title.Should().Be("Algebra book");
        result.Value.Category.Should().Be(Category.Textbook);
        result.Value.Condition.Should().Be(Condition.Good);
        result.Value.Price.Should().Be(25m);
        result.Value.Status.Should().Be(ListingStatus.Draft);
        result.Value.Version.Should().Be(1);
        result.Value.CreatedAt.Should().Be(Now);
        repository.Items.Should().HaveCount(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_InvalidTitle_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.NewGuid(), " ", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleEmpty");
        result.Error.Message.Should().Be("Title must not be empty.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InvalidPrice_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 0m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Money.Invalid");
        result.Error.Message.Should().Be("Price must be greater than 0.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptySellerId_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.Empty, "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.SellerEmpty");
        result.Error.Message.Should().Be("Seller id must not be empty.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_CommandWithDescription_When_Handling_Then_PassesDescriptionToListingTrimmed()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, "  Looks almost new.  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Looks almost new.");
        repository.Items.Single().Value.Description.Should().Be("Looks almost new.");
    }

    [Fact]
    public async Task Given_CommandWithoutDescription_When_Handling_Then_DescriptionIsNull()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
        repository.Items.Single().Value.Description.Should().BeNull();
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var description = new string('a', 1001);
        var command = new CreateListingCommand(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, description);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be("Description must have at most 1000 characters.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsConflictAndDoesNotSave()
    {
        var repository = new FakeListingRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Listing.Duplicate");
        result.Error.Message.Should().Match("A listing with id '*' already exists.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InactiveCategory_When_Handling_Then_ReturnsCategoryInactiveAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var categories = new FakeCategoryRepository();
        categories.Deactivate(Category.Notes);
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), categories);
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra notes", Category.Notes, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInactive");
        result.Error.Message.Should().Be("The category 'Notes' is not active, a listing cannot be put in it.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UndefinedCategory_When_Handling_Then_ReturnsCategoryInvalidAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", (Category)99, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInvalid");
        result.Error.Message.Should().Be("Unknown category.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NullCategory_When_Handling_Then_ReturnsCategoryRequiredAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", null, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryRequired");
        repository.Items.Should().BeEmpty();
        repository.AddCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NullCondition_When_Handling_Then_ReturnsConditionRequiredAndDoesNotStoreOrSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now), new FakeCategoryRepository());
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", Category.Textbook, null, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.ConditionRequired");
        repository.Items.Should().BeEmpty();
        repository.AddCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
