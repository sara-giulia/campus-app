using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Auth.Login;

public sealed class LoginHandler(
    IMemberRepository repository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    // Same error for a wrong email and a wrong password, so nobody can find out which emails exist.
    private static readonly Error InvalidCredentials =
        Error.Validation("Auth.InvalidCredentials", "The email or the password is wrong.");

    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var email = Email.Create(command.Email);
        if (email.IsFailure || string.IsNullOrEmpty(command.Password))
            return Result<LoginResponse>.Failure(InvalidCredentials);

        var members = await repository.GetAllAsync(ct);
        var member = members.FirstOrDefault(m => m.Email == email.Value);

        if (member?.PasswordHash is null || !passwordHasher.Verify(member.PasswordHash, command.Password))
            return Result<LoginResponse>.Failure(InvalidCredentials);

        var (token, expiresAt) = tokenService.CreateToken(member);

        return new LoginResponse(token, expiresAt);
    }
}

