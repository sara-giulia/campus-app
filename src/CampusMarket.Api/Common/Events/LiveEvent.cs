namespace CampusMarket.Api.Common.Events;

// One event sent to a logged-in member over GET /api/v1/members/me/events (Server-Sent Events).
// It only says that something new is there; the page then loads it through the normal endpoints,
// so the stream never carries the text of a message or of a notification.
public sealed record LiveEvent(string Type, Guid? ConversationId)
{
    public const string NotificationType = "notification";
    public const string MessageType = "message";
    public const string PingType = "ping";

    // A new notification for the member (reservation, sale, expired reservation).
    public static readonly LiveEvent Notification = new(NotificationType, null);

    // Sent when nothing else happened for a while, so proxies (Render) do not close an idle connection.
    public static readonly LiveEvent Ping = new(PingType, null);

    // A new message in one of the member's conversations, written by the other member.
    public static LiveEvent Message(Guid conversationId) => new(MessageType, conversationId);
}
