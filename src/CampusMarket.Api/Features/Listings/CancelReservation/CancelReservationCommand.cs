using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.CancelReservation;

public sealed record CancelReservationCommand(
    Guid ListingId,
    Guid ReservationId,
    Guid RequesterId,
    bool IsAdmin) : ICommand<Guid>;
