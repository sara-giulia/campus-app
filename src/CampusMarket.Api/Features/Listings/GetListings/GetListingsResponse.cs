using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

public sealed record GetListingsResponse(IReadOnlyList<GetListingsItem> Items, int Count);

public sealed record GetListingsItem(
    Guid Id,
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    ListingStatus Status)
{
    public static GetListingsItem From(Listing listing) => new(
        listing.Id,
        listing.SellerId,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status);
}