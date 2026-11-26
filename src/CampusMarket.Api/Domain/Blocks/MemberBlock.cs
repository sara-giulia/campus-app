using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Blocks;

// A member (the blocker) does not want to hear from, or see the listings of, another member (the blocked).
// One block per ordered pair (unique index in the database). The block works one way: the blocked member
// cannot write to, make offers to or reserve from the blocker; the blocker is not restricted.
public sealed class MemberBlock
{
    public Guid Id { get; private set; }
    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private MemberBlock() { }

    public static Result<MemberBlock> Create(Guid blockerId, Guid blockedId, DateTimeOffset createdAt)
    {
        if (blockerId == blockedId)
            return Result<MemberBlock>.Failure(
                Error.Validation("Block.Self", "You cannot block yourself."));

        return new MemberBlock
        {
            Id = Guid.NewGuid(),
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAt = createdAt
        };
    }

    // The error every slice answers (403) when the recipient of an action blocked the member who acts.
    public static Error BlockedError() =>
        Error.Forbidden("Member.Blocked", "This member has blocked you.");
}
