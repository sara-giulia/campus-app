using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.UpdateMember;

public sealed class UpdateMemberHandler(
    IMemberRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateMemberCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        UpdateMemberCommand command, CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(command.Id, ct);

        if (member is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.Id}' was not found."));

        var newEmail = Email.Create(command.Email);
        if (newEmail.IsSuccess)
        {
            var existingMembers = await repository.GetAllAsync(ct);
            if (existingMembers.Any(m => m.Id != member.Id && m.Email == newEmail.Value))
                return Result<Guid>.Failure(
                    Error.Conflict("Member.EmailTaken", $"A member with email '{newEmail.Value.Value}' already exists."));
        }

        var updated = member.Update(command.Name, command.Email);

        if (updated.IsFailure)
            return Result<Guid>.Failure(updated.Error);

        repository.Update(member);
        await unitOfWork.SaveChangesAsync(ct);

        return member.Id;
    }
}
