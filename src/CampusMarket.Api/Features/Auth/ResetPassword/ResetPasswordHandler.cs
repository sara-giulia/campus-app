using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Register;

namespace CampusMarket.Api.Features.Auth.ResetPassword;

// Validates the password the same way Register does (reuses MinPasswordLength
// and the same error codes), then verifies the reset token: unknown, expired
// or already used all map to Auth.InvalidResetToken (no leaking which it was).
// On success: hash the new password, mark the token used, revoke every active
// refresh token of the member so sessions from the old password can't survive,
// one CommitAsync, then email "password was changed" (body never contains it).
public sealed class ResetPasswordHandler(
    IPasswordResetTokenRepository tokens,
    IMemberRepository members,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    TimeProvider timeProvider)
    : ICommandHandler<ResetPasswordCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.NewPassword) || command.NewPassword.Length < RegisterHandler.MinPasswordLength)
            return Result<Guid>.Failure(
                Error.Validation("Auth.WeakPassword", $"Password must have at least {RegisterHandler.MinPasswordLength} characters."));

        if (command.NewPassword != command.ConfirmNewPassword)
            return Result<Guid>.Failure(
                Error.Validation("Auth.PasswordsDoNotMatch", "The two passwords are not the same."));

        if (string.IsNullOrWhiteSpace(command.Token))
            return Result<Guid>.Failure(PasswordResetToken.Invalid);

        var now = timeProvider.GetUtcNow();
        var token = await tokens.GetByHashAsync(PasswordResetToken.Hash(command.Token), ct);
        if (token is null || !token.IsValid(now))
            return Result<Guid>.Failure(PasswordResetToken.Invalid);

        var member = await members.GetByIdAsync(token.MemberId, ct);
        if (member is null)
            return Result<Guid>.Failure(PasswordResetToken.Invalid);

        var changeResult = member.ChangePassword(passwordHasher.Hash(command.NewPassword));
        if (changeResult.IsFailure)
            return Result<Guid>.Failure(changeResult.Error);

        token.MarkUsed(now);

        foreach (var active in await refreshTokens.GetActiveByMemberAsync(member.Id, now, ct))
        {
            active.Revoke(now);
            refreshTokens.Update(active);
        }

        members.Update(member);
        tokens.Update(token);
        await unitOfWork.CommitAsync(ct);

        var body =
            $"Hello {member.Name},{Environment.NewLine}{Environment.NewLine}" +
            "Your CampusMarket password was just changed. If this wasn't you, contact support right away." + Environment.NewLine;
        await emailSender.SendAsync(member.Email.Value, "Your CampusMarket password was changed", body, ct);

        return member.Id;
    }
}
