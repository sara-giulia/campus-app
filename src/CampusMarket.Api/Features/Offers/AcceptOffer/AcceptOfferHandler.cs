using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Features.Listings.ReserveListing;

namespace CampusMarket.Api.Features.Offers.AcceptOffer;

// The side that did not propose the offer accepts it. The listing (still Active) is reserved for the
// buyer through Listing.Reserve, with the agreed price on the reservation and the same limit of active
// reservations per buyer as POST /listings/{id}/reservations. Every other Pending offer on the listing
// is rejected. Both sides are notified, and so is every buyer whose offer was closed.
public sealed class AcceptOfferHandler(
    IOfferRepository offers,
    IListingRepository listings,
    IMemberRepository members,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<AcceptOfferCommand, AcceptOfferResponse>
{
    public async Task<Result<AcceptOfferResponse>> HandleAsync(AcceptOfferCommand command, CancellationToken ct)
    {
        var offer = await offers.GetByIdAsync(command.OfferId, ct);
        if (offer is null)
            return Result<AcceptOfferResponse>.Failure(
                Error.NotFound("Offer.NotFound", $"Offer '{command.OfferId}' was not found."));

        var canAnswer = offer.CheckCanAnswer(command.MemberId);
        if (canAnswer is not null)
            return Result<AcceptOfferResponse>.Failure(canAnswer);

        // The listing and both members exist: deleting one of them deletes the offer (cascade).
        var listing = (await listings.GetByIdAsync(offer.ListingId, ct))!;
        var member = (await members.GetByIdAsync(command.MemberId, ct))!;

        var allListings = await listings.GetAllAsync(ct);
        var activeReservations = allListings
            .SelectMany(l => l.Reservations)
            .Count(r => r.MemberId == offer.BuyerId && r.Status == ReservationStatus.Active);
        if (activeReservations >= ReserveListingHandler.MaxActiveReservations)
            return Result<AcceptOfferResponse>.Failure(
                Error.Validation("Offer.BuyerReservationLimit",
                    $"The buyer already holds {ReserveListingHandler.MaxActiveReservations} active reservations."));

        var now = timeProvider.GetUtcNow();
        var accepted = offer.Accept(command.MemberId, listing, now);
        if (accepted.IsFailure)
            return Result<AcceptOfferResponse>.Failure(accepted.Error);

        OfferNotifications.Add(notifications, offer.ProposedById, NotificationType.OfferAccepted,
            OfferNotifications.AcceptedByOther(member.Name, offer.Amount, listing.Title), listing.Id, now);
        OfferNotifications.Add(notifications, command.MemberId, NotificationType.OfferAccepted,
            OfferNotifications.AcceptedByYou(offer.Amount, listing.Title), listing.Id, now);

        // The listing is Reserved now: the other buyers' Pending offers cannot be accepted any more.
        foreach (var other in await offers.GetPendingForListingAsync(listing.Id, ct))
        {
            if (other.Id == offer.Id || !other.CloseBecauseListingReserved(now))
                continue;

            OfferNotifications.Add(notifications, other.BuyerId, NotificationType.OfferRejected,
                OfferNotifications.ClosedByOtherReservation(other.Amount, listing.Title), listing.Id, now);
        }

        // The listing is tracked (Version is its concurrency token, so two accepts at once cannot both
        // reserve it); EF Core inserts the new reservation by itself, as in ReserveListingHandler.
        await unitOfWork.CommitAsync(ct);

        return new AcceptOfferResponse(OfferResponse.From(offer), AcceptedReservation.From(accepted.Value));
    }
}
