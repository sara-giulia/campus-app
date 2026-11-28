namespace CampusMarket.Api.Domain.Reports;

public interface IMemberReportRepository
{
    /// <summary>
    /// Adds a report. Saved by IUnitOfWork.CommitAsync.
    /// </summary>
    void Add(MemberReport report);

    /// <summary>
    /// True when this reporter already has a Pending report on this member.
    /// </summary>
    Task<bool> HasPendingAsync(Guid reporterId, Guid reportedMemberId, CancellationToken ct = default);

    /// <summary>
    /// A snapshot of every Pending report, all members, in no particular order.
    /// </summary>
    Task<IReadOnlyList<MemberReport>> GetAllPendingAsync(CancellationToken ct = default);

    /// <summary>
    /// The Pending reports on this member, tracked so that confirming or dismissing them
    /// is saved by the next CommitAsync. Empty if there are none.
    /// </summary>
    Task<IReadOnlyList<MemberReport>> GetPendingByMemberAsync(Guid reportedMemberId, CancellationToken ct = default);
}
