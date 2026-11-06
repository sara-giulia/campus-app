using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfRefreshTokenRepository(CampusMarketDbContext db) : IRefreshTokenRepository
{
    public void Add(RefreshToken refreshToken) => db.RefreshTokens.Add(refreshToken);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Update(RefreshToken refreshToken) => db.RefreshTokens.Update(refreshToken);
}