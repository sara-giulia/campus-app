namespace CampusMarket.Api.Domain.Members;

public interface IRefreshTokenRepository
{
    /// Adds a new refresh token. It is saved by IUnitOfWork.CommitAsync.
    void Add(RefreshToken refreshToken);

    /// Returns the token with this SHA-256 hash, or null if there is none.
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// Saves the changes of a token loaded with GetByHashAsync (for example after Revoke)
    void Update(RefreshToken refreshToken);
}