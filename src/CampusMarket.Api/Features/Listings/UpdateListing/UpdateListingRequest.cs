using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

/// <summary>
/// The JSON body of PUT /api/v1/listings/{id}. The id comes from the route.
/// SuggestedCategory (optional): see CreateListingRequest.
/// </summary>
public sealed record UpdateListingRequest(
    string Title,
    Category? Category,
    Condition? Condition,
    decimal Price,
    string? Description = null,
    string? SuggestedCategory = null);
    