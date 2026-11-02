using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public sealed record CreateListingResponse(
    Guid Id,
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    ListingStatus Status,
    int Version,
    DateTimeOffset CreatedAt,
    string? Description)
{
    public static CreateListingResponse From(Listing listing) => new(
        listing.Id,
        listing.SellerId,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status,
        listing.Version,
        listing.CreatedAt,
        listing.Description);
}