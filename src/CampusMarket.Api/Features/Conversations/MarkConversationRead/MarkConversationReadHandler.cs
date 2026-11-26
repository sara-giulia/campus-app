using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Features.Conversations.MarkConversationRead;

// The page calls it when the member opens the conversation (and again when a new message arrives
// while it is open). Nothing is saved when everything was already read.
public sealed class MarkConversationReadHandler(
    IConversationRepository conversations,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MarkConversationReadCommand, int>
{
    public async Task<Result<int>> HandleAsync(MarkConversationReadCommand command, CancellationToken ct)
    {
        var conversation = await conversations.GetByIdAsync(command.ConversationId, ct);
        if (conversation is null)
            return Result<int>.Failure(
                Error.NotFound("Conversation.NotFound", $"Conversation '{command.ConversationId}' was not found."));

        var marked = conversation.MarkReadBy(command.MemberId);
        if (marked.IsSuccess && marked.Value > 0)
            await unitOfWork.CommitAsync(ct);

        return marked;
    }
}
