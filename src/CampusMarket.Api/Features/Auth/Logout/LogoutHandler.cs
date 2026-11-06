using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Auth.Logout;

public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<LogoutCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(LogoutCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return Result<Guid>.Failure(RefreshToken.Invalid);

        var now = timeProvider.GetUtcNow();
        var stored = await refreshTokens.GetByHashAsync(RefreshToken.Hash(command.RefreshToken), ct);

        if (stored is null || !stored.IsActive(now))
            return Result<Guid>.Failure(RefreshToken.Invalid);

        stored.Revoke(now);
        refreshTokens.Update(stored);
        await unitOfWork.CommitAsync(ct);

        return stored.Id;
    }
}