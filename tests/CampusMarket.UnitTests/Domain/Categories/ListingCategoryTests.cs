using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Categories;

public class ListingCategoryTests
{
    private static ListingCategory Build(Category key, bool isActive = true)
    {
        return ListingCategory.Create(key, key.ToString(), null, isActive).Value;
    }

    private static List<ListingCategory> AllActive()
    {
        return Enum.GetValues<Category>().Select(k => Build(k)).ToList();
    }

    [Fact]
    public void Given_ValidData_When_Creating_Then_ReturnsActiveCategoryWithTrimmedValues()
    {
        var result = ListingCategory.Create(Category.Textbook, "  Manuale  ", "  Cărți pentru cursuri  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().Be(Category.Textbook);
        result.Value.DisplayName.Should().Be("Manuale");
        result.Value.Description.Should().Be("Cărți pentru cursuri");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Given_IsActiveFalse_When_Creating_Then_ReturnsInactiveCategory()
    {
        var result = ListingCategory.Create(Category.Notes, "Notițe", null, isActive: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_BlankDescription_When_Creating_Then_StoresNullDescription(string description)
    {
        var result = ListingCategory.Create(Category.Notes, "Notițe", description);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public void Given_UndefinedKey_When_Creating_Then_ReturnsNotFound()
    {
        var result = ListingCategory.Create((Category)99, "Unknown");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Category.NotFound");
        result.Error.Message.Should().Be("Category '99' was not found.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyDisplayName_When_Creating_Then_ReturnsDisplayNameEmpty(string? displayName)
    {
        var result = ListingCategory.Create(Category.Textbook, displayName);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DisplayNameEmpty");
        result.Error.Message.Should().Be("Display name must not be empty.");
    }

    [Fact]
    public void Given_DisplayNameOfExactlyMaxLength_When_Creating_Then_Succeeds()
    {
        var displayName = new string('a', ListingCategory.MaxDisplayNameLength);

        var result = ListingCategory.Create(Category.Textbook, displayName);

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public void Given_DisplayNameOfMaxLengthWithSpacesAround_When_Creating_Then_Succeeds()
    {
        var displayName = new string('a', ListingCategory.MaxDisplayNameLength);

        var result = ListingCategory.Create(Category.Textbook, "  " + displayName + "  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public void Given_DisplayNameTooLong_When_Creating_Then_ReturnsDisplayNameTooLong()
    {
        var displayName = new string('a', ListingCategory.MaxDisplayNameLength + 1);

        var result = ListingCategory.Create(Category.Textbook, displayName);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DisplayNameTooLong");
        result.Error.Message.Should().Be($"Display name must have at most {ListingCategory.MaxDisplayNameLength} characters.");
    }

    [Fact]
    public void Given_DescriptionOfExactlyMaxLength_When_Creating_Then_Succeeds()
    {
        var description = new string('d', ListingCategory.MaxDescriptionLength);

        var result = ListingCategory.Create(Category.Textbook, "Manuale", description);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(description);
    }

    [Fact]
    public void Given_DescriptionTooLong_When_Creating_Then_ReturnsDescriptionTooLong()
    {
        var description = new string('d', ListingCategory.MaxDescriptionLength + 1);

        var result = ListingCategory.Create(Category.Textbook, "Manuale", description);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DescriptionTooLong");
        result.Error.Message.Should().Be($"Description must have at most {ListingCategory.MaxDescriptionLength} characters.");
    }

    [Fact]
    public void Given_ActiveCategory_When_ChangingDisplayNameAndDescription_Then_StoresTrimmedValuesAndReturnsSameInstance()
    {
        var categories = AllActive();
        var category = categories[0];

        var result = category.Update("  Manuale noi  ", "  Cărți și culegeri  ", true, categories);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(category);
        category.DisplayName.Should().Be("Manuale noi");
        category.Description.Should().Be("Cărți și culegeri");
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Given_CategoryWithDescription_When_UpdatingWithNullDescription_Then_DescriptionIsNull()
    {
        var category = ListingCategory.Create(Category.Textbook, "Manuale", "Cărți").Value;
        var categories = new List<ListingCategory> { category };

        var result = category.Update("Manuale", null, true, categories);

        result.IsSuccess.Should().BeTrue();
        category.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_ActiveCategory_When_UpdatingWithEmptyDisplayName_Then_ReturnsDisplayNameEmptyAndChangesNothing(string? displayName)
    {
        var categories = AllActive();
        var category = categories[0];

        var result = category.Update(displayName, "New description", false, categories);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DisplayNameEmpty");
        result.Error.Message.Should().Be("Display name must not be empty.");
        category.DisplayName.Should().Be("Textbook");
        category.Description.Should().BeNull();
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Given_ActiveCategory_When_UpdatingWithTooLongDisplayName_Then_ReturnsDisplayNameTooLongAndChangesNothing()
    {
        var categories = AllActive();
        var category = categories[0];
        var displayName = new string('a', ListingCategory.MaxDisplayNameLength + 1);

        var result = category.Update(displayName, null, true, categories);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DisplayNameTooLong");
        result.Error.Message.Should().Be($"Display name must have at most {ListingCategory.MaxDisplayNameLength} characters.");
        category.DisplayName.Should().Be("Textbook");
    }

    [Fact]
    public void Given_ActiveCategory_When_UpdatingWithTooLongDescription_Then_ReturnsDescriptionTooLongAndChangesNothing()
    {
        var categories = AllActive();
        var category = categories[0];
        var description = new string('d', ListingCategory.MaxDescriptionLength + 1);

        var result = category.Update("Manuale", description, true, categories);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Category.DescriptionTooLong");
        result.Error.Message.Should().Be($"Description must have at most {ListingCategory.MaxDescriptionLength} characters.");
        category.DisplayName.Should().Be("Textbook");
        category.Description.Should().BeNull();
    }

    [Fact]
    public void Given_AnotherActiveCategory_When_Deactivating_Then_CategoryIsInactive()
    {
        var categories = AllActive();
        var category = categories[0];

        var result = category.Update("Textbook", null, false, categories);

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Given_InactiveCategory_When_Reactivating_Then_CategoryIsActive()
    {
        var category = Build(Category.Textbook, isActive: false);
        var categories = new List<ListingCategory> { category, Build(Category.Notes) };

        var result = category.Update("Textbook", null, true, categories);

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Given_LastActiveCategory_When_Deactivating_Then_ReturnsLastActiveAndChangesNothing()
    {
        var category = Build(Category.Textbook);
        var categories = new List<ListingCategory>
        {
            category,
            Build(Category.Notes, isActive: false),
            Build(Category.Electronics, isActive: false),
            Build(Category.LabEquipment, isActive: false),
            Build(Category.Other, isActive: false)
        };

        var result = category.Update("Manuale noi", "New description", false, categories);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Category.LastActive");
        result.Error.Message.Should().Be("At least one category must stay active.");
        category.IsActive.Should().BeTrue();
        category.DisplayName.Should().Be("Textbook");
        category.Description.Should().BeNull();
    }

    [Fact]
    public void Given_LastActiveCategory_When_KeepingItActive_Then_Succeeds()
    {
        var category = Build(Category.Textbook);
        var categories = new List<ListingCategory> { category, Build(Category.Notes, isActive: false) };

        var result = category.Update("Manuale", null, true, categories);

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeTrue();
        category.DisplayName.Should().Be("Manuale");
    }

    [Fact]
    public void Given_InactiveCategoryAndNoOtherActive_When_KeepingItInactive_Then_Succeeds()
    {
        var category = Build(Category.Textbook, isActive: false);
        var categories = new List<ListingCategory> { category, Build(Category.Notes, isActive: false) };

        var result = category.Update("Manuale", null, false, categories);

        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
        category.DisplayName.Should().Be("Manuale");
    }

    [Fact]
    public void Given_ActiveCategory_When_CheckingAcceptsListings_Then_ReturnsNull()
    {
        var category = Build(Category.Textbook);

        var error = category.CheckAcceptsListings();

        error.Should().BeNull();
    }

    [Fact]
    public void Given_InactiveCategory_When_CheckingAcceptsListings_Then_ReturnsCategoryInactive()
    {
        var category = Build(Category.Electronics, isActive: false);

        var error = category.CheckAcceptsListings();

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Listing.CategoryInactive");
        error.Message.Should().Be("The category 'Electronics' is not active, a listing cannot be put in it.");
    }

    [Fact]
    public void Given_Key_When_BuildingNotFound_Then_ReturnsNotFoundErrorWithKey()
    {
        var error = ListingCategory.NotFound("Books");

        error.Type.Should().Be(ErrorType.NotFound);
        error.Code.Should().Be("Category.NotFound");
        error.Message.Should().Be("Category 'Books' was not found.");
    }
}
