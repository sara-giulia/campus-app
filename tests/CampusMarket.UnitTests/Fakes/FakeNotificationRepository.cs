using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = new();

    public void Add(Notification notification) => Items.Add(notification);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(n => n.Id == id));

    public Task<List<Notification>> GetByMemberAsync(Guid memberId, CancellationToken ct = default) =>
        Task.FromResult(Items
            .Where(n => n.MemberId == memberId)
            .OrderByDescending(n => n.CreatedAt)
            .ToList());

    public Task<List<Notification>> GetUnreadForMemberAsync(Guid memberId, CancellationToken ct = default) =>
        Task.FromResult(Items.Where(n => n.MemberId == memberId && !n.IsRead).ToList());

    public Task<int> CountUnreadAsync(Guid memberId, CancellationToken ct = default) =>
        Task.FromResult(Items.Count(n => n.MemberId == memberId && !n.IsRead));
}
