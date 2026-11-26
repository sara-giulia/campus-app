namespace CampusMarket.Api.Domain.Blocks;

public interface IMemberBlockRepository
{
    /// <summary>
    /// Adds a block. Saved by IUnitOfWork.CommitAsync.
    /// </summary>
    void Add(MemberBlock block);

    /// <summary>
    /// The block of this blocker on this member, tracked, or null.
    /// </summary>
    Task<MemberBlock?> GetAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default);

    /// <summary>
    /// True when the blocker has blocked the member.
    /// </summary>
    Task<bool> IsBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default);

    /// <summary>
    /// A snapshot of every block made by the member, the newest first.
    /// </summary>
    Task<IReadOnlyList<MemberBlock>> GetByBlockerAsync(Guid blockerId, CancellationToken ct = default);

    /// <summary>
    /// The ids of the members the blocker has blocked.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetBlockedIdsAsync(Guid blockerId, CancellationToken ct = default);

    void Delete(MemberBlock block);
}
