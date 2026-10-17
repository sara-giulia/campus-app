using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

/// <summary>
/// The JSON body of POST /api/v1/listings
/// </summary>
public sealed record CreateListingRequest(
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price);