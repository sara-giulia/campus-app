namespace CampusMarket.Api.Features.Conversations.GetMyConversations;

// Every conversation of the member, as buyer or as seller, the one with the latest message first.
public sealed record GetMyConversationsResponse(IReadOnlyList<MyConversationItem> Items, int Count);

// The other member is the seller when I am the buyer, and the buyer when I am the seller.
// UnreadCount: the messages of the other member I have not seen yet (0 = the conversation is read).
public sealed record MyConversationItem(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    Guid OtherMemberId,
    string OtherMemberName,
    string LastMessage,
    Guid LastMessageSenderId,
    bool LastMessageIsSuspicious,
    DateTimeOffset LastMessageAt,
    int MessageCount,
    int UnreadCount);
