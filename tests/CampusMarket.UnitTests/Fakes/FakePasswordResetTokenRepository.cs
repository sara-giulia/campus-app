using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
{
    public Dictionary<Guid, PasswordResetToken> Items { get; } = new();

    public int UpdateCount { get; private set; }

    public void Add(PasswordResetToken token)
    {
        Items[token.Id] = token;
    }

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = Items.Values.FirstOrDefault(t => t.TokenHash == tokenHash);
        return Task.FromResult(token);
    }

    public void Update(PasswordResetToken token)
    {
        Items[token.Id] = token;
        UpdateCount++;
    }
}
