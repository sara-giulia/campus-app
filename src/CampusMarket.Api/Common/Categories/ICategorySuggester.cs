using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Common.Categories;

// Suggests the category of a listing from its title and optional description,
// with a confidence between 0 and 1. The implementation lives in Infrastructure/ML;
// this interface keeps the handler free of Microsoft.ML types.
public interface ICategorySuggester
{
    CategorySuggestion Suggest(string title, string? description);
}

public sealed record CategorySuggestion(Category Category, double Confidence);
