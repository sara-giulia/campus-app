using CampusMarket.Api.Domain.Blocks;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeMemberBlockRepository : IMemberBlockRepository
{
    public List<MemberBlock> Items { get; } = new();

    public void Add(MemberBlock block) => Items.Add(block);

    public Task<MemberBlock?> GetAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(b => b.BlockerId == blockerId && b.BlockedId == blockedId));

    public Task<bool> IsBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(b => b.BlockerId == blockerId && b.BlockedId == blockedId));

    public Task<IReadOnlyList<MemberBlock>> GetByBlockerAsync(Guid blockerId, CancellationToken ct = default)
    {
        IReadOnlyList<MemberBlock> blocks = Items
            .Where(b => b.BlockerId == blockerId)
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .ToList();
        return Task.FromResult(blocks);
    }

    public Task<IReadOnlySet<Guid>> GetBlockedIdsAsync(Guid blockerId, CancellationToken ct = default)
    {
        IReadOnlySet<Guid> ids = Items.Where(b => b.BlockerId == blockerId).Select(b => b.BlockedId).ToHashSet();
        return Task.FromResult(ids);
    }

    public void Delete(MemberBlock block) => Items.Remove(block);
}
