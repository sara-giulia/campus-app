using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Offers.AcceptOffer;

// The member comes from the token.
public sealed record AcceptOfferCommand(Guid MemberId, Guid OfferId) : ICommand<AcceptOfferResponse>;

// The accepted offer and the reservation it created at the agreed price.
public sealed record AcceptOfferResponse(OfferResponse Offer, AcceptedReservation Reservation);

public sealed record AcceptedReservation(
    Guid Id,
    Guid ListingId,
    Guid BuyerId,
    decimal AgreedPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    ReservationStatus Status)
{
    public static AcceptedReservation From(Reservation reservation) => new(
        reservation.Id,
        reservation.ListingId,
        reservation.MemberId,
        reservation.AgreedPrice!.Value,
        reservation.CreatedAt,
        reservation.ExpiresAt,
        reservation.Status);
}
