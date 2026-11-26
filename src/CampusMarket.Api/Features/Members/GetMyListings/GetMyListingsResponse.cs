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
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    // What the active reservation's buyer pays (the price agreed through an offer, or the listing's price);
    // null when the listing has no active reservation.
    decimal? ReservedPrice)
{
    public static MyListingItem From(Listing listing) => new(
        listing.Id,
        listing.SellerId,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status,
        listing.CreatedAt,
        listing.ExpiresAt,
        listing.Reservations.FirstOrDefault(r => r.Status == ReservationStatus.Active)?.PriceFor(listing));
}
