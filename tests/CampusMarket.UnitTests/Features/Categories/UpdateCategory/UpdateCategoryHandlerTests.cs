using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Categories.UpdateCategory;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Categories.UpdateCategory;

public class UpdateCategoryHandlerTests
{
    [Fact]
    public async Task Given_ExistingCategory_When_HandlingValidCommand_Then_ChangesValuesUpdatesAndSavesOnce()
    {
        var repository = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryHandler(repository, unitOfWork);
        var command = new UpdateCategoryCommand("textbook", "  Manuale noi  ", "  Cărți pentru cursuri  ", false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Category.Textbook);
        repository.Items[Category.Textbook].DisplayName.Should().Be("Manuale noi");
        repository.Items[Category.Textbook].Description.Should().Be("Cărți pentru cursuri");
        repository.Items[Category.Textbook].IsActive.Should().BeFalse();
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Books")]
    [InlineData("1")]
    public async Task Given_UnknownKey_When_Handling_Then_ReturnsNotFoundAndDoesNotSave(string key)
    {
        var repository = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryHandler(repository, unitOfWork);
        var command = new UpdateCategoryCommand(key, "Manuale", null, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Category.NotFound");
        result.Error.Message.Should().Be($"Category '{key}' was not found.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_KeyNotInRepository_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeCategoryRepository();
        repository.Items.Remove(Category.Notes);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryHandler(repository, unitOfWork);
        var command = new UpdateCategoryCommand("Notes", "Notițe", null, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Category.NotFound");
        result.Error.Message.Should().Be("Category 'Notes' was not found.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MissingIsActive_When_Handling_Then_ReturnsIsActiveRequiredAndChangesNothing()
    {
        var repository = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryHandler(repository, unitOfWork);
        var command = new UpdateCategoryCommand("Textbook", "Manuale noi", null, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.IsActiveRequired");
        result.Error.Message.Should().Be("isActive is required.");
        repository.Items[Category.Textbook].DisplayName.Should().Be("Manuale");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyDisplayName_When_Handling_Then_ReturnsDisplayNameEmptyAndChangesNothing()
    {
        var repository = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryHandler(repository, unitOfWork);
        var command = new UpdateCategoryCommand("Textbook", "   ", "New description", false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DisplayNameEmpty");
        result.Error.Message.Should().Be("Display name must not be empty.");
        repository.Items[Category.Textbook].DisplayName.Should().Be("Manuale");
        repository.Items[Category.Textbook].Description.Should().BeNull();
        repository.Items[Category.Textbook].IsActive.Should().BeTrue();
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_LastActiveCategory_When_Deactivating_Then_ReturnsLastActiveAndDoesNotSave()
    {
        var repository = new FakeCategoryRepository();
        repository.Deactivate(Category.Notes);
        repository.Deactivate(Category.Electronics);
        repository.Deactivate(Category.LabEquipment);
        repository.Deactivate(Category.Other);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryHandler(repository, unitOfWork);
        var command = new UpdateCategoryCommand("Textbook", "Manuale noi", null, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Category.LastActive");
        result.Error.Message.Should().Be("At least one category must stay active.");
        repository.Items[Category.Textbook].IsActive.Should().BeTrue();
        repository.Items[Category.Textbook].DisplayName.Should().Be("Manuale");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
