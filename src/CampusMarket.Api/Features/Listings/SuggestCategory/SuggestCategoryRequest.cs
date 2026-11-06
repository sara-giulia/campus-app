namespace CampusMarket.Api.Features.Listings.SuggestCategory;

// The JSON body of POST /api/v1/listings/category-suggestion. Description is optional,
// the same way it is on a listing. No category is sent in: this slice is exactly the
// one that finds it.
public sealed record SuggestCategoryRequest(string Title, string? Description = null);
