using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Features.Conversations.GetConversation;

// The conversation with every message, the oldest first.
public sealed record GetConversationResponse(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    Guid SellerId,
    string SellerName,
    Guid BuyerId,
    string BuyerName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ConversationMessage> Messages);

public sealed record ConversationMessage(Guid Id, int Number, Guid SenderId, string Text, DateTimeOffset SentAt)
{
    public static ConversationMessage From(Message message) => new(
        message.Id, message.Number, message.SenderId, message.Text, message.SentAt);
}
