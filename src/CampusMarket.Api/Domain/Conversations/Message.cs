namespace CampusMarket.Api.Domain.Conversations;

// One message of a conversation. Number is 1, 2, 3… in the order they were sent, so the order is
// the same even when two messages have the same time.
public sealed class Message
{
    public const int MaxTextLength = 1000;

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public int Number { get; private set; }
    public Guid SenderId { get; private set; }
    // Stryker disable once String : the internal constructor always sets Text and EF Core overwrites it, so the default value is never observable
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }

    private Message() { }

    internal Message(Guid conversationId, int number, Guid senderId, string text, DateTimeOffset sentAt)
    {
        Id = Guid.NewGuid();
        ConversationId = conversationId;
        Number = number;
        SenderId = senderId;
        Text = text;
        SentAt = sentAt;
    }
}
