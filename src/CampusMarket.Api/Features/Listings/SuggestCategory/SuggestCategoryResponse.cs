using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.SuggestCategory;

// Confidence is between 0 and 1, with two decimals. It is only a suggestion:
// the seller still picks the category when creating the listing.
// Category is null when the model is not sure enough (confidence below Ml:Category:MinConfidence, see CategorySuggestionOptions).
public sealed record SuggestCategoryResponse(Category? Category, double Confidence);
