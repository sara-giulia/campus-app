using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Register;

namespace CampusMarket.Api.Features.Auth.ChangePassword;

public sealed class ChangePasswordHandler(
    IMemberRepository repository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangePasswordCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ChangePasswordCommand command, CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(command.MemberId, ct);
        if (member is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.MemberId}' was not found."));

        // 1. The current password must be right (same error as a failed login).
        if (member.PasswordHash is null || !passwordHasher.Verify(member.PasswordHash, command.CurrentPassword))
            return Result<Guid>.Failure(
                Error.Validation("Auth.InvalidCredentials", "The email or the password is wrong."));

        // 2. The same rules as Register.
        if (string.IsNullOrEmpty(command.NewPassword) || command.NewPassword.Length < RegisterHandler.MinPasswordLength)
            return Result<Guid>.Failure(
                Error.Validation("Auth.WeakPassword", $"Password must have at least {RegisterHandler.MinPasswordLength} characters."));

        if (command.NewPassword != command.ConfirmNewPassword)
            return Result<Guid>.Failure(
                Error.Validation("Auth.PasswordsDoNotMatch", "The two passwords are not the same."));

        var changed = member.ChangePassword(passwordHasher.Hash(command.NewPassword));
        if (changed.IsFailure)
            return Result<Guid>.Failure(changed.Error);

        repository.Update(member);
        await unitOfWork.CommitAsync(ct);

        return member.Id;
    }
}