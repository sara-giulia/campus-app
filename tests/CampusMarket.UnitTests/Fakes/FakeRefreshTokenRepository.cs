using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public Dictionary<Guid, RefreshToken> Items { get; } = new();

    public int UpdateCount { get; private set; }

    public void Add(RefreshToken refreshToken)
    {
        Items[refreshToken.Id] = refreshToken;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var refreshToken = Items.Values.FirstOrDefault(t => t.TokenHash == tokenHash);
        return Task.FromResult(refreshToken);
    }

    public void Update(RefreshToken refreshToken)
    {
        Items[refreshToken.Id] = refreshToken;
        UpdateCount++;
    }

    public Task<List<RefreshToken>> GetActiveByMemberAsync(
        Guid memberId, DateTimeOffset now, CancellationToken ct = default)
    {
        var active = Items.Values
            .Where(t => t.MemberId == memberId && t.IsActive(now))
            .ToList();
        return Task.FromResult(active);
    }
}
