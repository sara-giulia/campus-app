using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.Api.Features.Offers.RejectOffer;

// The side that did not propose the offer refuses it; the side that proposed it is notified.
public sealed class RejectOfferHandler(
    IOfferRepository offers,
    IListingRepository listings,
    IMemberRepository members,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RejectOfferCommand, OfferResponse>
{
    public async Task<Result<OfferResponse>> HandleAsync(RejectOfferCommand command, CancellationToken ct)
    {
        var offer = await offers.GetByIdAsync(command.OfferId, ct);
        if (offer is null)
            return Result<OfferResponse>.Failure(
                Error.NotFound("Offer.NotFound", $"Offer '{command.OfferId}' was not found."));

        var now = timeProvider.GetUtcNow();
        var rejected = offer.Reject(command.MemberId, now);
        if (rejected.IsFailure)
            return Result<OfferResponse>.Failure(rejected.Error);

        // The listing and both members exist: deleting one of them deletes the offer (cascade).
        var listing = (await listings.GetByIdAsync(offer.ListingId, ct))!;
        var member = (await members.GetByIdAsync(command.MemberId, ct))!;

        OfferNotifications.Add(notifications, offer.ProposedById, NotificationType.OfferRejected,
            OfferNotifications.Rejected(member.Name, offer.Amount, listing.Title), listing.Id, now);

        await unitOfWork.CommitAsync(ct);

        return OfferResponse.From(offer);
    }
}
