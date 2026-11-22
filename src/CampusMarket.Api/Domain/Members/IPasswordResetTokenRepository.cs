namespace CampusMarket.Api.Domain.Members;

public interface IPasswordResetTokenRepository
{
    /// Adds a new reset token. Saved by the next IUnitOfWork.CommitAsync.
    void Add(PasswordResetToken token);

    /// Returns the token with this SHA-256 hash, or null if there is none.
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// Saves the changes of a token loaded with GetByHashAsync (for example after MarkUsed).
    void Update(PasswordResetToken token);
}
