using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.SuggestPrice;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.SuggestPrice;

public class SuggestPriceHandlerTests
{
    [Fact]
    public async Task Given_ValidQuery_When_Handling_Then_ReturnsSuggesterValueAndPassesTrimmedTitle()
    {
        var suggester = new FakePriceSuggester { SuggestedPrice = 250 };
        var handler = new SuggestPriceHandler(suggester);
        var query = new SuggestPriceQuery("  Casio fx-991EX  ", Category.Electronics, Condition.Good, "Scientific calculator");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuggestedPrice.Should().Be(250);
        suggester.CallCount.Should().Be(1);
        suggester.LastTitle.Should().Be("Casio fx-991EX");
        suggester.LastDescription.Should().Be("Scientific calculator");
        suggester.LastCategory.Should().Be(Category.Electronics);
        suggester.LastCondition.Should().Be(Condition.Good);
    }

    [Fact]
    public async Task Given_NullDescription_When_Handling_Then_PassesNullDescriptionToSuggester()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var query = new SuggestPriceQuery("Algebra book", Category.Textbook, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
        suggester.LastDescription.Should().BeNull();
    }

    [Fact]
    public async Task Given_UnknownCategory_When_Handling_Then_ReturnsCategoryInvalidAndSuggesterNotCalled()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var query = new SuggestPriceQuery("Algebra book", (Category)99, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInvalid");
        result.Error.Message.Should().Be("Unknown category.");
        suggester.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownCondition_When_Handling_Then_ReturnsConditionInvalidAndSuggesterNotCalled()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var query = new SuggestPriceQuery("Algebra book", Category.Textbook, (Condition)99);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.ConditionInvalid");
        result.Error.Message.Should().Be("Unknown condition.");
        suggester.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_Handling_Then_ReturnsDescriptionTooLongAndSuggesterNotCalled()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var description = new string('a', Listing.MaxDescriptionLength + 1);
        var query = new SuggestPriceQuery("Algebra book", Category.Textbook, Condition.Good, description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be($"Description must have at most {Listing.MaxDescriptionLength} characters.");
        suggester.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DescriptionAtMaxLength_When_Handling_Then_ReturnsSuccess()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var description = new string('a', Listing.MaxDescriptionLength);
        var query = new SuggestPriceQuery("Algebra book", Category.Textbook, Condition.Good, description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DescriptionAtMaxLengthWithSurroundingSpaces_When_Handling_Then_ReturnsSuccess()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var description = "  " + new string('a', Listing.MaxDescriptionLength) + "  ";
        var query = new SuggestPriceQuery("Algebra book", Category.Textbook, Condition.Good, description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_EmptyOrWhitespaceTitle_When_Handling_Then_ReturnsTitleEmptyAndSuggesterNotCalled(string title)
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var query = new SuggestPriceQuery(title, Category.Textbook, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleEmpty");
        result.Error.Message.Should().Be("Title must not be empty.");
        suggester.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TitleTooLong_When_Handling_Then_ReturnsTitleTooLongAndSuggesterNotCalled()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var title = new string('a', Listing.MaxTitleLength + 1);
        var query = new SuggestPriceQuery(title, Category.Textbook, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleTooLong");
        result.Error.Message.Should().Be($"Title must have at most {Listing.MaxTitleLength} characters.");
        suggester.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TitleAtMaxLength_When_Handling_Then_ReturnsSuccess()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var title = new string('a', Listing.MaxTitleLength);
        var query = new SuggestPriceQuery(title, Category.Textbook, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_TitleAtMaxLengthWithSurroundingSpaces_When_Handling_Then_ReturnsSuccess()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var title = "  " + new string('a', Listing.MaxTitleLength) + "  ";
        var query = new SuggestPriceQuery(title, Category.Textbook, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_InvalidCategoryAndEmptyTitle_When_Handling_Then_ReturnsCategoryInvalid()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var query = new SuggestPriceQuery("", (Category)99, Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInvalid");
        result.Error.Message.Should().Be("Unknown category.");
        suggester.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DescriptionTooLongAndEmptyTitle_When_Handling_Then_ReturnsDescriptionTooLong()
    {
        var suggester = new FakePriceSuggester();
        var handler = new SuggestPriceHandler(suggester);
        var description = new string('a', Listing.MaxDescriptionLength + 1);
        var query = new SuggestPriceQuery("", Category.Textbook, Condition.Good, description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be($"Description must have at most {Listing.MaxDescriptionLength} characters.");
        suggester.CallCount.Should().Be(0);
    }
}
