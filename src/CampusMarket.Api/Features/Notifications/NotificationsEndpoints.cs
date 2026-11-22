using CampusMarket.Api.Features.Notifications.GetMyNotifications;
using CampusMarket.Api.Features.Notifications.MarkAllNotificationsRead;
using CampusMarket.Api.Features.Notifications.MarkNotificationRead;

namespace CampusMarket.Api.Features.Notifications;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Sits under /api/v1/members/me/notifications (the task's chosen URL shape),
        // but the slice lives next to the other notification code.
        var group = app.MapGroup("/members/me/notifications");

        GetMyNotificationsEndpoint.Map(group);
        MarkNotificationReadEndpoint.Map(group);
        MarkAllNotificationsReadEndpoint.Map(group);

        return app;
    }
}
