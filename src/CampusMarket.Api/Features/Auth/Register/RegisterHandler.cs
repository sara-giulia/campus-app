using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Auth.Register;

public sealed class RegisterHandler(
    IMemberRepository repository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public const int MinPasswordLength = 8;

    public async Task<Result<RegisterResponse>> HandleAsync(
        RegisterCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.Password) || command.Password.Length < MinPasswordLength)
            return Result<RegisterResponse>.Failure(
                Error.Validation("Auth.WeakPassword", $"Password must have at least {MinPasswordLength} characters."));

        var registered = Member.Register(
            command.Name,
            command.Email,
            passwordHasher.Hash(command.Password),
            timeProvider.GetUtcNow());

        if (registered.IsFailure)
            return Result<RegisterResponse>.Failure(registered.Error);

        var member = registered.Value;

        var existingMembers = await repository.GetAllAsync(ct);
        if (existingMembers.Any(m => m.Email == member.Email))
            return Result<RegisterResponse>.Failure(
                Error.Conflict("Member.EmailTaken", $"A member with email '{member.Email.Value}' already exists."));

        var added = await repository.AddAsync(member, ct);
        if (!added)
            return Result<RegisterResponse>.Failure(
                Error.Conflict("Member.Duplicate", $"A member with id '{member.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return RegisterResponse.From(member);
    }
}

