using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Notifications;

public sealed class Notification
{
    public const int MaxMessageLength = 500;

    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    public NotificationType Type { get; private set; }
    // Stryker disable once String : the factory always sets Message, so the default value is never observable
    public string Message { get; private set; } = string.Empty;
    public Guid? ListingId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification() { }

    public static Result<Notification> Create(
        Guid memberId,
        NotificationType type,
        string message,
        Guid? listingId,
        DateTimeOffset createdAt)
    {
        if (memberId == Guid.Empty)
            return Result<Notification>.Failure(
                Error.Validation("Notification.MemberEmpty", "Member id must not be empty."));

        if (!Enum.IsDefined(type))
            return Result<Notification>.Failure(
                Error.Validation("Notification.TypeInvalid", "Unknown notification type."));

        if (string.IsNullOrWhiteSpace(message))
            return Result<Notification>.Failure(
                Error.Validation("Notification.MessageEmpty", "Message must not be empty."));

        if (message.Trim().Length > MaxMessageLength)
            return Result<Notification>.Failure(
                Error.Validation("Notification.MessageTooLong", $"Message must have at most {MaxMessageLength} characters."));

        return new Notification
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Type = type,
            Message = message.Trim(),
            ListingId = listingId,
            CreatedAt = createdAt,
            ReadAt = null
        };
    }

    // Idempotent: a second call keeps the first ReadAt.
    public void MarkAsRead(DateTimeOffset now)
    {
        if (ReadAt is null)
            ReadAt = now;
    }

    public bool IsRead => ReadAt is not null;
}
