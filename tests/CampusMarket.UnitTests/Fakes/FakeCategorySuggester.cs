using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeCategorySuggester : ICategorySuggester
{
    public CategorySuggestion Suggestion { get; set; } = new(Category.Electronics, 0.87);

    public int CallCount { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastDescription { get; private set; }

    public CategorySuggestion Suggest(string title, string? description)
    {
        CallCount++;
        LastTitle = title;
        LastDescription = description;
        return Suggestion;
    }
}
