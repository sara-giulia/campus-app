using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadHandler(
    INotificationRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<MarkNotificationReadCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        MarkNotificationReadCommand command, CancellationToken ct)
    {
        var notification = await repository.GetByIdAsync(command.NotificationId, ct);

        // A notification that does not exist, or belongs to someone else, is a 404 to the caller:
        // the endpoint must not reveal that a notification with that id exists for another member.
        if (notification is null || notification.MemberId != command.MemberId)
            return Result<Guid>.Failure(
                Error.NotFound("Notification.NotFound", $"Notification '{command.NotificationId}' was not found."));

        notification.MarkAsRead(timeProvider.GetUtcNow());
        await unitOfWork.CommitAsync(ct);

        return notification.Id;
    }
}
