using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Notifications.GetMyNotifications;

public sealed record GetMyNotificationsQuery(
    Guid MemberId,
    bool UnreadOnly = false,
    int Page = GetMyNotificationsQuery.DefaultPage,
    int PageSize = GetMyNotificationsQuery.DefaultPageSize)
    : IQuery<GetMyNotificationsResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
