using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Members.GetMyReservations;

public sealed record GetMyReservationsResponse(IReadOnlyList<MyReservationItem> Items, int Count);

public sealed record MyReservationItem(
    Guid ReservationId,
    Guid ListingId,
    string ListingTitle,
    decimal Price,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
