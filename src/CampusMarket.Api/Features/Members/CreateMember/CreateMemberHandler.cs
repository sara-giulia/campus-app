using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.CreateMember;

public sealed class CreateMemberHandler(
    IMemberRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateMemberCommand, CreateMemberResponse>
{
    public async Task<Result<CreateMemberResponse>> HandleAsync(
        CreateMemberCommand command, CancellationToken ct)
    {
        var created = Member.Create(command.Name, command.Email, timeProvider.GetUtcNow());

        if (created.IsFailure)
            return Result<CreateMemberResponse>.Failure(created.Error);

        var member = created.Value;

        var existingMembers = await repository.GetAllAsync(ct);
        if (existingMembers.Any(m => m.Email == member.Email))
            return Result<CreateMemberResponse>.Failure(
                Error.Conflict("Member.EmailTaken", $"A member with email '{member.Email.Value}' already exists."));

        var added = await repository.AddAsync(member, ct);
        if (!added)
            return Result<CreateMemberResponse>.Failure(
                Error.Conflict("Member.Duplicate", $"A member with id '{member.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return CreateMemberResponse.From(member);
    }
}
