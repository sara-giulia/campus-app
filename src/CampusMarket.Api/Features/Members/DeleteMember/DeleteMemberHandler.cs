using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.DeleteMember;

public sealed class DeleteMemberHandler(
    IMemberRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteMemberCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        DeleteMemberCommand command, CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(command.Id, ct);

        if (member is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.Id}' was not found."));

        if (member.Id != command.RequesterId && !command.IsAdmin)
            return Result<Guid>.Failure(
                Error.Forbidden("Member.NotOwner", "Only the member or an Admin can delete this profile."));

        repository.Delete(member);
        await unitOfWork.CommitAsync(ct);

        return member.Id;
    }
}
