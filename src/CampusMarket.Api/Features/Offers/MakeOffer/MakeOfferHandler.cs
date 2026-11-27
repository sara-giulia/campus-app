using System.Globalization;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.Api.Features.Offers.MakeOffer;

// The buyer proposes a price for an Active listing. The offer lives in the buyer's conversation about
// the listing; when there is none yet, the offer starts it with a short message, so the seller sees it.
public sealed class MakeOfferHandler(
    IListingRepository listings,
    IMemberRepository members,
    IOfferRepository offers,
    IConversationRepository conversations,
    INotificationRepository notifications,
    IMemberBlockRepository blocks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<MakeOfferCommand, MakeOfferResponse>
{
    public async Task<Result<MakeOfferResponse>> HandleAsync(MakeOfferCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        // The listing of a suspended seller is not public while the suspension lasts.
        if (listing is null || await members.IsSuspendedAsync(listing.SellerId, timeProvider.GetUtcNow(), ct))
            return Result<MakeOfferResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var buyer = await members.GetByIdAsync(command.BuyerId, ct);
        if (buyer is null)
            return Result<MakeOfferResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.BuyerId}' was not found."));

        if (await blocks.IsBlockedAsync(listing.SellerId, command.BuyerId, ct))
            return Result<MakeOfferResponse>.Failure(MemberBlock.BlockedError());

        var now = timeProvider.GetUtcNow();
        var made = Offer.Make(listing, command.BuyerId, command.Amount, now);
        if (made.IsFailure)
            return Result<MakeOfferResponse>.Failure(made.Error);

        if (await offers.HasPendingAsync(listing.Id, command.BuyerId, ct))
            return Result<MakeOfferResponse>.Failure(
                Error.Conflict("Offer.AlreadyPending", "There is already a pending offer between you and the seller on this listing."));

        var conversation = await conversations.GetByListingAndBuyerAsync(listing.Id, command.BuyerId, ct);
        if (conversation is null)
        {
            var amount = command.Amount.ToString("0.##", CultureInfo.InvariantCulture);
            // Cannot fail here: Offer.Make already checked the buyer (not empty, not the seller) and the
            // listing (Active), and the message is fixed text.
            conversation = Conversation.Start(command.BuyerId, listing, $"Am făcut o ofertă de {amount} lei.", now).Value;
            await conversations.AddAsync(conversation, ct);
        }

        var offer = made.Value;
        offers.Add(offer);

        OfferNotifications.Add(notifications, listing.SellerId, NotificationType.OfferReceived,
            OfferNotifications.Received(buyer.Name, offer.Amount, listing.Title), listing.Id, now);

        await unitOfWork.CommitAsync(ct);

        return new MakeOfferResponse(conversation.Id, OfferResponse.From(offer));
    }
}
