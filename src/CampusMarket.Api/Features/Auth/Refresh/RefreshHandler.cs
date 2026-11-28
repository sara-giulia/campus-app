using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Auth.Refresh;

public sealed class RefreshHandler(
    IRefreshTokenRepository refreshTokens,
    IMemberRepository members,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RefreshCommand, RefreshResponse>
{
    public async Task<Result<RefreshResponse>> HandleAsync(RefreshCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return Result<RefreshResponse>.Failure(RefreshToken.Invalid);

        var now = timeProvider.GetUtcNow();
        var stored = await refreshTokens.GetByHashAsync(RefreshToken.Hash(command.RefreshToken), ct);

        // Unknown, expired, revoked by logout or already used by an earlier refresh: the same 401.
        if (stored is null || !stored.IsActive(now))
            return Result<RefreshResponse>.Failure(RefreshToken.Invalid);

        var member = await members.GetByIdAsync(stored.MemberId, ct);
        if (member is null)
            return Result<RefreshResponse>.Failure(RefreshToken.Invalid);

        // Suspending revokes every refresh token already; this covers a token created in between.
        // The token is used up, and the answer (401) says why, so the frontend can show it.
        if (member.IsSuspended(now))
        {
            stored.Revoke(now);
            refreshTokens.Update(stored);
            await unitOfWork.CommitAsync(ct);
            return Result<RefreshResponse>.Failure(member.AccountSuspendedError());
        }

        // Rotation: the old refresh token can never be used again.
        stored.Revoke(now);
        refreshTokens.Update(stored);

        var newRefreshToken = tokenService.CreateRefreshToken();
        refreshTokens.Add(RefreshToken.Create(member.Id, newRefreshToken, now));

        var (token, expiresAt) = tokenService.CreateToken(member);
        await unitOfWork.CommitAsync(ct);

        return new RefreshResponse(token, expiresAt, newRefreshToken);
    }
}