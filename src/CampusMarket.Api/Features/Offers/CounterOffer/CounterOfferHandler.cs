using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.Api.Features.Offers.CounterOffer;

// The side that did not propose the offer answers with another amount: the offer becomes Countered and
// a new Pending offer goes to the other side (who is notified). The listing must still be Active.
public sealed class CounterOfferHandler(
    IOfferRepository offers,
    IListingRepository listings,
    IMemberRepository members,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CounterOfferCommand, OfferResponse>
{
    public async Task<Result<OfferResponse>> HandleAsync(CounterOfferCommand command, CancellationToken ct)
    {
        var offer = await offers.GetByIdAsync(command.OfferId, ct);
        if (offer is null)
            return Result<OfferResponse>.Failure(
                Error.NotFound("Offer.NotFound", $"Offer '{command.OfferId}' was not found."));

        // The listing and both members exist: deleting one of them deletes the offer (cascade).
        var listing = (await listings.GetByIdAsync(offer.ListingId, ct))!;

        var now = timeProvider.GetUtcNow();
        var countered = offer.Counter(command.MemberId, listing, command.Amount, now);
        if (countered.IsFailure)
            return Result<OfferResponse>.Failure(countered.Error);

        var counter = countered.Value;
        offers.Add(counter);

        var member = (await members.GetByIdAsync(command.MemberId, ct))!;
        OfferNotifications.Add(notifications, counter.RecipientId, NotificationType.OfferCountered,
            OfferNotifications.Countered(member.Name, counter.Amount, listing.Title), listing.Id, now);

        await unitOfWork.CommitAsync(ct);

        return OfferResponse.From(counter);
    }
}
