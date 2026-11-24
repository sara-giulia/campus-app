using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Members.GetMyListings;

public sealed record GetMyListingsResponse(IReadOnlyList<MyListingItem> Items, int Count);

public sealed record MyListingItem(
    Guid Id,
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    ListingStatus Status,
    DateTimeOffset CreatedAt)
{
    public static MyListingItem From(Listing listing) => new(
        listing.Id,
        listing.SellerId,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status,
        listing.CreatedAt);
}
