using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.Api.Features.Blocks.BlockMember;

// The blocker stops the other member from writing to them, making offers to them or reserving their
// listings. Blocking twice is fine (nothing changes). The Pending offers between the two are rejected
// (without a notification, so the block stays private); messages and reservations stay.
public sealed class BlockMemberHandler(
    IMemberBlockRepository blocks,
    IMemberRepository members,
    IOfferRepository offers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<BlockMemberCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(BlockMemberCommand command, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var block = MemberBlock.Create(command.BlockerId, command.BlockedId, now);
        if (block.IsFailure)
            return Result<Guid>.Failure(block.Error);

        if (await members.GetByIdAsync(command.BlockedId, ct) is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.BlockedId}' was not found."));

        if (await blocks.IsBlockedAsync(command.BlockerId, command.BlockedId, ct))
            return command.BlockedId;

        blocks.Add(block.Value);

        foreach (var offer in await offers.GetPendingBetweenAsync(command.BlockerId, command.BlockedId, ct))
            offer.CloseBecauseBlocked(now);

        await unitOfWork.CommitAsync(ct);

        return command.BlockedId;
    }
}
