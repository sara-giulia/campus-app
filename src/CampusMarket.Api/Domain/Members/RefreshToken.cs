using System.Security.Cryptography;
using System.Text;
using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Members;

/// A long random token that gives a new access token without the password.
/// Only its SHA-256 hash is stored, so a copy of the database cannot be used to log in.
public sealed class RefreshToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // Same answer for an unknown, expired, revoked or already used token.
    public static readonly Error Invalid =
        Error.Validation("Auth.InvalidRefreshToken", "The refresh token is not valid.");

    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    // Stryker disable once String : the factory always sets TokenHash, so the default value is never observable
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid memberId, string token, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        TokenHash = Hash(token),
        CreatedAt = now,
        ExpiresAt = now.Add(Lifetime)
    };

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Active = not revoked (refresh and logout revoke it) and not expired.
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}