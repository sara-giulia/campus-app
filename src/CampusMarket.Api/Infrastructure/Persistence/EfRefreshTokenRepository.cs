using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfRefreshTokenRepository(CampusMarketDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken refreshToken) => db.RefreshTokens.Add(refreshToken);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Update(RefreshToken refreshToken) => db.RefreshTokens.Update(refreshToken);

    // IsActive keeps the rule in the domain: pull the member's
    // tokens (MemberId is indexed) and filter with the domain's IsActive.
    public async Task<List<RefreshToken>> GetActiveByMemberAsync(
        Guid memberId, DateTimeOffset now, CancellationToken ct = default)
    {
        var all = await db.RefreshTokens
            .Where(t => t.MemberId == memberId)
            .ToListAsync(ct);
        return all.Where(t => t.IsActive(now)).ToList();
    }
}