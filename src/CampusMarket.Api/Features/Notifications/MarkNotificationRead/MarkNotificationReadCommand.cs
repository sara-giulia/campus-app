using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Notifications.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId, Guid MemberId) : ICommand<Guid>;
