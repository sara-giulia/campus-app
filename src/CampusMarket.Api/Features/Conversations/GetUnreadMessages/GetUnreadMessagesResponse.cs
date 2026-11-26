namespace CampusMarket.Api.Features.Conversations.GetUnreadMessages;

// For the red badge on "Mesaje": the messages from the other side not seen yet, and in how many conversations.
public sealed record GetUnreadMessagesResponse(int UnreadMessages, int UnreadConversations);
