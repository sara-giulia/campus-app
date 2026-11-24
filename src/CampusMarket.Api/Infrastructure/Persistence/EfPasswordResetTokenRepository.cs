using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfPasswordResetTokenRepository(CampusMarketDbContext db) : IPasswordResetTokenRepository
{
    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Update(PasswordResetToken token) => db.PasswordResetTokens.Update(token);
}
