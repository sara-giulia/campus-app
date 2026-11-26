using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Features.Conversations.SendMessage;

// The buyer or the seller answers in a conversation they are part of.
public sealed class SendMessageHandler(
    IConversationRepository conversations,
    IMemberBlockRepository blocks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<SendMessageCommand, MessageResponse>
{
    public async Task<Result<MessageResponse>> HandleAsync(SendMessageCommand command, CancellationToken ct)
    {
        var conversation = await conversations.GetByIdAsync(command.ConversationId, ct);
        if (conversation is null)
            return Result<MessageResponse>.Failure(
                Error.NotFound("Conversation.NotFound", $"Conversation '{command.ConversationId}' was not found."));

        // The other side may have blocked the sender; a stranger gets the NotParticipant error from Send.
        if (conversation.IsParticipant(command.SenderId)
            && await blocks.IsBlockedAsync(conversation.OtherParticipant(command.SenderId), command.SenderId, ct))
            return Result<MessageResponse>.Failure(MemberBlock.BlockedError());

        var sent = conversation.Send(command.SenderId, command.Text, timeProvider.GetUtcNow());
        if (sent.IsFailure)
            return Result<MessageResponse>.Failure(sent.Error);

        await unitOfWork.CommitAsync(ct);

        return MessageResponse.From(sent.Value);
    }
}
