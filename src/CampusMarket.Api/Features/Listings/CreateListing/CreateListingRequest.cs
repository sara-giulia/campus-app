using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

/// <summary>
/// The JSON body of POST /api/v1/listings. The seller is the logged-in member, from the token.
/// SuggestedCategory (optional): the category the suggestion endpoint showed to the seller. It is text, not
/// the enum, so a missing or unknown value never fails the request; it only feeds the training examples.
/// </summary>
public sealed record CreateListingRequest(
    string Title,
    Category? Category,
    Condition? Condition,
    decimal Price,
    string? Description = null,
    string? SuggestedCategory = null);