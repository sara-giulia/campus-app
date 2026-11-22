using System.Security.Cryptography;
using System.Text;
using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Members;

// One-shot token sent by email when a member asks to reset their password.
// Only the SHA-256 hash is stored, so a copy of the database cannot be used
// to reset passwords. The factory returns the plain token once; the caller
// mails it immediately and never persists it.
public sealed class PasswordResetToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    public const int TokenByteLength = 32;

    // Same answer for an unknown, expired or already-used token: never leak
    // which of the three it was.
    public static readonly Error Invalid =
        Error.Validation("Auth.InvalidResetToken", "The reset token is not valid.");

    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    // Stryker disable once String : the factory always sets TokenHash, so the default value is never observable
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    private PasswordResetToken() { }

    // Returns the token aggregate to save and the plain-text token to mail to
    // the member. The plain token exists only in memory for the lifetime of
    // the request; the database only sees Hash(plain).
    public static (PasswordResetToken Token, string PlainToken) Create(Guid memberId, DateTimeOffset now)
    {
        var plain = CreatePlainToken();
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            TokenHash = Hash(plain),
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime),
        };
        return (token, plain);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool IsValid(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;

    // 32 random bytes encoded as URL-safe Base64 (no padding, '+' -> '-', '/' -> '_').
    private static string CreatePlainToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
