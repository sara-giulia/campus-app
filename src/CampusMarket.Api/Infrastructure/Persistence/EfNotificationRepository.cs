using CampusMarket.Api.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfNotificationRepository(CampusMarketDbContext db) : INotificationRepository
{
    public void Add(Notification notification) => db.Notifications.Add(notification);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<List<Notification>> GetByMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        // Sorted in the database (PostgreSQL orders timestamptz columns).
        var notifications = await db.Notifications
            .AsNoTracking()
            .Where(n => n.MemberId == memberId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .ToListAsync(ct);

        return notifications;
    }

    public async Task<List<Notification>> GetUnreadForMemberAsync(Guid memberId, CancellationToken ct = default) =>
        await db.Notifications
            .Where(n => n.MemberId == memberId && n.ReadAt == null)
            .ToListAsync(ct);

    public Task<int> CountUnreadAsync(Guid memberId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.MemberId == memberId && n.ReadAt == null, ct);
}
