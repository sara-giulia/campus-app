using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.UpdateMyProfile;

public sealed class UpdateMyProfileHandler(IMemberRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateMyProfileCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UpdateMyProfileCommand command, CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(command.MemberId, ct);

        if (member is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.MemberId}' was not found."));

        // Only the name changes here; the email stays the same.
        var updated = member.Update(command.Name, member.Email.Value);
        if (updated.IsFailure)
            return Result<Guid>.Failure(updated.Error);

        repository.Update(member);
        await unitOfWork.CommitAsync(ct);

        return member.Id;
    }
}