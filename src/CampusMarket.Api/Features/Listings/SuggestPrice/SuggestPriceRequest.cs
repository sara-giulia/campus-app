using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.SuggestPrice;

// The JSON body of POST /api/v1/listings/price-suggestion. Description is optional,
// the same way it is on a listing. No price is sent in: this slice is exactly the
// one that computes a price.
public sealed record SuggestPriceRequest(
    string Title,
    Category? Category,
    Condition? Condition,
    string? Description = null);
