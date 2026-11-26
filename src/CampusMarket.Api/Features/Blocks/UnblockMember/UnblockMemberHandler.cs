using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Features.Blocks.UnblockMember;

// Removes the block. Unblocking someone who is not blocked (or does not exist) is fine: nothing changes.
public sealed class UnblockMemberHandler(
    IMemberBlockRepository blocks,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UnblockMemberCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(UnblockMemberCommand command, CancellationToken ct)
    {
        var block = await blocks.GetAsync(command.BlockerId, command.BlockedId, ct);
        if (block is null)
            return command.BlockedId;

        blocks.Delete(block);
        await unitOfWork.CommitAsync(ct);

        return command.BlockedId;
    }
}
