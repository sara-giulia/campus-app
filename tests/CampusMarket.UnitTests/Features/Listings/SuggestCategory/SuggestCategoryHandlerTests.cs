using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.SuggestCategory;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.SuggestCategory;

public class SuggestCategoryHandlerTests
{
    [Fact]
    public async Task Given_ValidQuery_When_Handling_Then_ReturnsSuggesterValuesAndPassesTrimmedTitle()
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var query = new SuggestCategoryQuery("  Laptop Dell Latitude  ", "Used laptop, 16 GB RAM");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be(suggester.Suggestion.Category);
        result.Value.Confidence.Should().Be(suggester.Suggestion.Confidence);
        suggester.CallCount.Should().Be(1);
        suggester.LastTitle.Should().Be("Laptop Dell Latitude");
        suggester.LastDescription.Should().Be("Used laptop, 16 GB RAM");
    }

    [Fact]
    public async Task Given_SuggesterReturnsNotes_When_Handling_Then_ReturnsSuggesterCategoryAndConfidence()
    {
        var suggester = new FakeCategorySuggester { Suggestion = new CategorySuggestion(Category.Notes, 0.5) };
        var handler = new SuggestCategoryHandler(suggester);
        var query = new SuggestCategoryQuery("Algebra notes", "Handwritten, first semester");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be(Category.Notes);
        result.Value.Confidence.Should().Be(0.5);
    }

    [Fact]
    public async Task Given_NullDescription_When_Handling_Then_PassesNullDescriptionToSuggester()
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var query = new SuggestCategoryQuery("Laptop Dell Latitude");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
        suggester.LastDescription.Should().BeNull();
    }

    [Fact]
    public async Task Given_DescriptionTooLong_When_Handling_Then_ReturnsDescriptionTooLongAndSuggesterNotCalled()
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var description = new string('a', Listing.MaxDescriptionLength + 1);
        var query = new SuggestCategoryQuery("Laptop Dell Latitude", description);

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
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var description = new string('a', Listing.MaxDescriptionLength);
        var query = new SuggestCategoryQuery("Laptop Dell Latitude", description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DescriptionAtMaxLengthWithSurroundingSpaces_When_Handling_Then_ReturnsSuccess()
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var description = "  " + new string('a', Listing.MaxDescriptionLength) + "  ";
        var query = new SuggestCategoryQuery("Laptop Dell Latitude", description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_EmptyOrWhitespaceTitle_When_Handling_Then_ReturnsTitleEmptyAndSuggesterNotCalled(string title)
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var query = new SuggestCategoryQuery(title);

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
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var title = new string('a', Listing.MaxTitleLength + 1);
        var query = new SuggestCategoryQuery(title);

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
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var title = new string('a', Listing.MaxTitleLength);
        var query = new SuggestCategoryQuery(title);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_TitleAtMaxLengthWithSurroundingSpaces_When_Handling_Then_ReturnsSuccess()
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var title = "  " + new string('a', Listing.MaxTitleLength) + "  ";
        var query = new SuggestCategoryQuery(title);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        suggester.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DescriptionTooLongAndEmptyTitle_When_Handling_Then_ReturnsDescriptionTooLong()
    {
        var suggester = new FakeCategorySuggester();
        var handler = new SuggestCategoryHandler(suggester);
        var description = new string('a', Listing.MaxDescriptionLength + 1);
        var query = new SuggestCategoryQuery("", description);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be($"Description must have at most {Listing.MaxDescriptionLength} characters.");
        suggester.CallCount.Should().Be(0);
    }
}
