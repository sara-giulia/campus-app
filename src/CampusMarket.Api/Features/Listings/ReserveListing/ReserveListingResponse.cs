using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.ReserveListing;

public sealed record ReserveListingResponse(
    Guid Id,
    Guid ListingId,
    Guid BuyerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    ReservationStatus Status)
{
    public static ReserveListingResponse From(Reservation reservation) => new(
        reservation.Id,
        reservation.ListingId,
        reservation.MemberId,
        reservation.CreatedAt,
        reservation.ExpiresAt,
        reservation.Status);
}
