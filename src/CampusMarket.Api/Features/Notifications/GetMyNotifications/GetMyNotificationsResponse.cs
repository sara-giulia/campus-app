using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Notifications.GetMyNotifications;

public sealed record GetMyNotificationsResponse(
    IReadOnlyList<NotificationItem> Items,
    int Count,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize);

public sealed record NotificationItem(
    Guid Id,
    NotificationType Type,
    string Message,
    Guid? ListingId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    public static NotificationItem From(Notification notification) => new(
        notification.Id,
        notification.Type,
        notification.Message,
        notification.ListingId,
        notification.CreatedAt,
        notification.ReadAt);
}
