using CampusMarket.Api.Common.CQRS;
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
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<StartConversationCommand, MessageResponse>
{
    public async Task<Result<MessageResponse>> HandleAsync(StartConversationCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        if (listing is null)
            return Result<MessageResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var member = await members.GetByIdAsync(command.BuyerId, ct);
        if (member is null)
            return Result<MessageResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.BuyerId}' was not found."));

        if (await blocks.IsBlockedAsync(listing.SellerId, command.BuyerId, ct))
            return Result<MessageResponse>.Failure(MemberBlock.BlockedError());

        var now = timeProvider.GetUtcNow();
        Message message;

        var existing = await conversations.GetByListingAndBuyerAsync(command.ListingId, command.BuyerId, ct);
        if (existing is not null)
        {
            var sent = existing.Send(command.BuyerId, command.Text, now);
            if (sent.IsFailure)
                return Result<MessageResponse>.Failure(sent.Error);
            message = sent.Value;
        }
        else
        {
            var started = Conversation.Start(command.BuyerId, listing, command.Text, now);
            if (started.IsFailure)
                return Result<MessageResponse>.Failure(started.Error);
            await conversations.AddAsync(started.Value, ct);
            message = started.Value.Messages[0];
        }

        await unitOfWork.CommitAsync(ct);

        return MessageResponse.From(message);
    }
}
