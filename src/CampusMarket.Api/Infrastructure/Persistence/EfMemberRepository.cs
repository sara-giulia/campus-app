using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfMemberRepository(CampusMarketDbContext db) : IMemberRepository
{
    public async Task<bool> AddAsync(Member member, CancellationToken ct = default)
    {
        if (await db.Members.AnyAsync(m => m.Id == member.Id, ct))
            return false;

        db.Members.Add(member);
        return true;
    }

    public Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default) =>
        await db.Members
            .AsNoTracking()
            .ToListAsync(ct);

    public void Update(Member member) => db.Members.Update(member);

    public void Delete(Member member) => db.Members.Remove(member);

    // Only the rows with a suspension (few), then the domain decides which one is still running,
    // so the rule (temporary vs permanent, end date) stays in Member.IsSuspended.
    public async Task<IReadOnlySet<Guid>> GetSuspendedIdsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var suspended = await db.Members
            .AsNoTracking()
            .Where(m => m.SuspendedAt != null)
            .ToListAsync(ct);
        return suspended.Where(m => m.IsSuspended(now)).Select(m => m.Id).ToHashSet();
    }
}
