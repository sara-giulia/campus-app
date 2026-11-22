namespace CampusMarket.Api.Domain.Notifications;

public interface INotificationRepository
{
    /// <summary>
    /// Adds a notification to the current unit of work. Saved by the next CommitAsync.
    /// </summary>
    void Add(Notification notification);

    /// <summary>
    /// Returns the notification, or null if it does not exist (the endpoint answers 404).
    /// The returned notification is tracked, so changing it is saved by the next CommitAsync.
    /// </summary>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns every notification of this member, newest first (CreatedAt descending).
    /// Read-only snapshot: the returned list is not tracked.
    /// </summary>
    Task<List<Notification>> GetByMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Returns the member's unread notifications, tracked, so MarkAsRead on each one
    /// is saved by the next CommitAsync.
    /// </summary>
    Task<List<Notification>> GetUnreadForMemberAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of unread notifications of this member.
    /// </summary>
    Task<int> CountUnreadAsync(Guid memberId, CancellationToken ct = default);
}
