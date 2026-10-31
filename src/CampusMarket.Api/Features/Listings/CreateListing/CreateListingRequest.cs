using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

/// <summary>
/// The JSON body of POST /api/v1/listings. The seller is the logged-in member, from the token.
/// </summary>
public sealed record CreateListingRequest(
    string Title,
    Category Category,
    Condition Condition,
    decimal Price);