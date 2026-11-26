using CampusMarket.Api.Domain.Blocks;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfMemberBlockRepository(CampusMarketDbContext db) : IMemberBlockRepository
{
    public void Add(MemberBlock block) => db.MemberBlocks.Add(block);

    public Task<MemberBlock?> GetAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default) =>
        db.MemberBlocks.FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, ct);

    public Task<bool> IsBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default) =>
        db.MemberBlocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, ct);

    public async Task<IReadOnlyList<MemberBlock>> GetByBlockerAsync(Guid blockerId, CancellationToken ct = default) =>
        await db.MemberBlocks
            .AsNoTracking()
            .Where(b => b.BlockerId == blockerId)
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> GetBlockedIdsAsync(Guid blockerId, CancellationToken ct = default) =>
        (await db.MemberBlocks
            .AsNoTracking()
            .Where(b => b.BlockerId == blockerId)
            .Select(b => b.BlockedId)
            .ToListAsync(ct)).ToHashSet();

    public void Delete(MemberBlock block) => db.MemberBlocks.Remove(block);
}
