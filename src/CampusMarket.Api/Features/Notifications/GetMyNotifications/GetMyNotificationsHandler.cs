using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Notifications.GetMyNotifications;

public sealed class GetMyNotificationsHandler(INotificationRepository repository)
    : IQueryHandler<GetMyNotificationsQuery, GetMyNotificationsResponse>
{
    public async Task<Result<GetMyNotificationsResponse>> HandleAsync(
        GetMyNotificationsQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<GetMyNotificationsResponse>.Failure(
                Error.Validation("Notifications.InvalidPage", "Page must be at least 1."));
        if (query.PageSize < 1 || query.PageSize > GetMyNotificationsQuery.MaxPageSize)
            return Result<GetMyNotificationsResponse>.Failure(
                Error.Validation("Notifications.InvalidPageSize",
                    $"Page size must be between 1 and {GetMyNotificationsQuery.MaxPageSize}."));

        var all = await repository.GetByMemberAsync(query.MemberId, ct);
        // UnreadCount always reflects every unread notification of the member,
        // regardless of unreadOnly and paging.
        var unreadCount = await repository.CountUnreadAsync(query.MemberId, ct);

        var filtered = query.UnreadOnly
            ? all.Where(n => !n.IsRead).ToList()
            : all;

        var items = filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(NotificationItem.From)
            .ToList();

        return new GetMyNotificationsResponse(
            items,
            items.Count,
            filtered.Count,
            unreadCount,
            query.Page,
            query.PageSize);
    }
}
