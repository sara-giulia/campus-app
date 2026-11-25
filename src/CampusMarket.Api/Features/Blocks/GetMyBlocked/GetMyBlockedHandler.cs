using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Blocks.GetMyBlocked;

// The members the caller blocked, the most recently blocked first.
public sealed class GetMyBlockedHandler(
    IMemberBlockRepository blocks,
    IMemberRepository members)
    : IQueryHandler<GetMyBlockedQuery, GetMyBlockedResponse>
{
    public async Task<Result<GetMyBlockedResponse>> HandleAsync(GetMyBlockedQuery query, CancellationToken ct)
    {
        var mine = await blocks.GetByBlockerAsync(query.MemberId, ct);
        var membersById = (await members.GetAllAsync(ct)).ToDictionary(m => m.Id);

        // Deleting a member deletes the blocks on them (cascade), so every block has its member.
        var items = mine
            .Select(b => new BlockedMemberItem(b.BlockedId, membersById[b.BlockedId].Name, b.CreatedAt))
            .ToList();

        return new GetMyBlockedResponse(items);
    }
}
