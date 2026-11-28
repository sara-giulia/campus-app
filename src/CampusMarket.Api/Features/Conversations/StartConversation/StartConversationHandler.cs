using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Conversations.StartConversation;

// The buyer writes to the seller about a listing. The first message starts the conversation; if the
// buyer already has one about this listing, the message is added to it, so the button on the listing
// page always works the same way.
public sealed class StartConversationHandler(
    IListingRepository listings,
    IMemberRepository members,
    IConversationRepository conversations,
    IMemberBlockRepository blocks,
    IMessageSpamDetector spamDetector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<StartConversationCommand, MessageResponse>
{
    public async Task<Result<MessageResponse>> HandleAsync(StartConversationCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        // The listing of a suspended seller is not public while the suspension lasts.
        if (listing is null || await members.IsSuspendedAsync(listing.SellerId, timeProvider.GetUtcNow(), ct))
            return Result<MessageResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var member = await members.GetByIdAsync(command.BuyerId, ct);
        if (member is null)
            return Result<MessageResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.BuyerId}' was not found."));

        if (await blocks.IsBlockedAsync(listing.SellerId, command.BuyerId, ct))
            return Result<MessageResponse>.Failure(MemberBlock.BlockedError());

        var now = timeProvider.GetUtcNow();

        var existing = await conversations.GetByListingAndBuyerAsync(command.ListingId, command.BuyerId, ct);
        Conversation? started = null;
        Result<Message> written;
        if (existing is not null)
        {
            written = existing.Send(command.BuyerId, command.Text, now);
        }
        else
        {
            var start = Conversation.Start(command.BuyerId, listing, command.Text, now);
            if (start.IsFailure)
                return Result<MessageResponse>.Failure(start.Error);
            started = start.Value;
            written = started.Messages[0];
        }

        if (written.IsFailure)
            return Result<MessageResponse>.Failure(written.Error);

        // A blocked message is not saved: nothing is committed and a new conversation is not added.
        var message = MessageSpamGuard.Check(spamDetector, written.Value);
        if (message.IsFailure)
            return Result<MessageResponse>.Failure(message.Error);

        if (started is not null)
            await conversations.AddAsync(started, ct);

        await unitOfWork.CommitAsync(ct);

        return MessageResponse.From(message.Value);
    }
}
