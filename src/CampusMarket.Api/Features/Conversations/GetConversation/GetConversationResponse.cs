using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Features.Offers;

namespace CampusMarket.Api.Features.Conversations.GetConversation;

// The conversation with every message and every price offer, the oldest first. UnreadCount: the
// messages of the other member the reader has not seen yet. Reading does not change it: the page
// calls POST /conversations/{id}/read when it is above 0.
public sealed record GetConversationResponse(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    Guid SellerId,
    string SellerName,
    Guid BuyerId,
    string BuyerName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ConversationMessage> Messages,
    IReadOnlyList<OfferResponse> Offers,
    int UnreadCount);

// IsSuspicious: the spam check flagged the message, the app shows it with a warning.
public sealed record ConversationMessage(Guid Id, int Number, Guid SenderId, string Text, DateTimeOffset SentAt, bool IsSuspicious)
{
    public static ConversationMessage From(Message message) => new(
        message.Id, message.Number, message.SenderId, message.Text, message.SentAt, message.IsSuspicious);
}
