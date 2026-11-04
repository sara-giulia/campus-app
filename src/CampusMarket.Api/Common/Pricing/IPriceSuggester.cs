using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Common.Pricing;

// Suggests a price (whole lei, at least 1) for a listing described by title,
// optional description, category and condition. The implementation lives in
// Infrastructure/ML; this interface keeps the handler free of Microsoft.ML types.
public interface IPriceSuggester
{
    int Suggest(string title, string? description, Category category, Condition condition);
}
