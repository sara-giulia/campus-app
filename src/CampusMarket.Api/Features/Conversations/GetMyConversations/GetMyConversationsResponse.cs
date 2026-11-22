namespace CampusMarket.Api.Features.Conversations.GetMyConversations;

// Every conversation of the member, as buyer or as seller, the one with the latest message first.
public sealed record GetMyConversationsResponse(IReadOnlyList<MyConversationItem> Items, int Count);

// The other member is the seller when I am the buyer, and the buyer when I am the seller.
public sealed record MyConversationItem(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    Guid OtherMemberId,
    string OtherMemberName,
    string LastMessage,
    Guid LastMessageSenderId,
    DateTimeOffset LastMessageAt,
    int MessageCount);
