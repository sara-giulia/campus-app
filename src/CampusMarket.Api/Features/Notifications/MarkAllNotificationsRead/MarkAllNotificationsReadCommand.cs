using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Notifications.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand(Guid MemberId) : ICommand<int>;
