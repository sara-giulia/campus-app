using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Members.GetMyReservations;

public sealed record GetMyReservationsResponse(IReadOnlyList<MyReservationItem> Items, int Count);

public sealed record MyReservationItem(
    Guid ReservationId,
    Guid ListingId,
    string ListingTitle,
    // The listing's price.
    decimal Price,
    // The price agreed through an accepted offer; null when the reservation was made at the listing's price.
    decimal? AgreedPrice,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    // True when the member has already reviewed this listing (only possible after a completed sale).
    bool Reviewed);
