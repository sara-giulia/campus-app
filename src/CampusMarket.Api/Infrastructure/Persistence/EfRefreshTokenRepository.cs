using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfRefreshTokenRepository(CampusMarketDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken refreshToken) => db.RefreshTokens.Add(refreshToken);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Update(RefreshToken refreshToken) => db.RefreshTokens.Update(refreshToken);

    public async Task<List<RefreshToken>> GetActiveByMemberAsync(
        Guid memberId, DateTimeOffset now, CancellationToken ct = default) =>
        await db.RefreshTokens
            .Where(t => t.MemberId == memberId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
}