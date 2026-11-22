using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadHandler(
    INotificationRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<MarkAllNotificationsReadCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        MarkAllNotificationsReadCommand command, CancellationToken ct)
    {
        var unread = await repository.GetUnreadForMemberAsync(command.MemberId, ct);
        if (unread.Count == 0)
            return 0;

        var now = timeProvider.GetUtcNow();
        foreach (var notification in unread)
            notification.MarkAsRead(now);

        await unitOfWork.CommitAsync(ct);
        return unread.Count;
    }
}
