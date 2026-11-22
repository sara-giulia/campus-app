using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Features.Conversations;

// The message that was just sent; the same answer for the first message and for the next ones.
public sealed record MessageResponse(Guid ConversationId, Guid Id, int Number, Guid SenderId, string Text, DateTimeOffset SentAt)
{
    public static MessageResponse From(Message message) => new(
        message.ConversationId, message.Id, message.Number, message.SenderId, message.Text, message.SentAt);
}
