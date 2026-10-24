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
}
