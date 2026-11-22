using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Listings.ExpireReservations;

public sealed class ExpireReservationsHandler(
    IListingRepository repository,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ExpireReservationsCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        ExpireReservationsCommand command, CancellationToken ct)
    {
        var listings = await repository.GetAllAsync(ct);
        var now = timeProvider.GetUtcNow();
        var total = 0;

        foreach (var listing in listings)
        {
            // Pin the active reservation (and its buyer) before expiring the listing,
            // so the notification goes to the right member.
            // Stryker disable once Logical : IsExpired already requires Status == Active and a listing has at most one Active reservation, so || selects the same reservation as &&
            var active = listing.Reservations.FirstOrDefault(r => r.Status == ReservationStatus.Active && r.IsExpired(now));
            var expired = listing.ExpireReservations(now);
            if (expired == 0)
                continue;

            repository.Update(listing);
            total += expired;

            if (active is not null)
            {
                var notification = Notification.Create(
                    active.MemberId,
                    NotificationType.ReservationExpired,
                    $"Your reservation for {listing.Title} expired",
                    listing.Id,
                    now);
                if (notification.IsSuccess)
                    notifications.Add(notification.Value);
            }
        }

        if (total > 0)
            await unitOfWork.CommitAsync(ct);

        return total;
    }
}
