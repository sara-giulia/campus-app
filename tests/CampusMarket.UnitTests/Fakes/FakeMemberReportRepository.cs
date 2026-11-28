using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeMemberReportRepository : IMemberReportRepository
{
    public List<MemberReport> Items { get; } = new();

    public void Add(MemberReport report) => Items.Add(report);

    public Task<bool> HasPendingAsync(Guid reporterId, Guid reportedMemberId, CancellationToken ct = default) =>
        Task.FromResult(Items.Any(r => r.ReporterId == reporterId && r.ReportedMemberId == reportedMemberId && r.IsPending));

    public Task<IReadOnlyList<MemberReport>> GetAllPendingAsync(CancellationToken ct = default)
    {
        IReadOnlyList<MemberReport> pending = Items.Where(r => r.IsPending).ToList();
        return Task.FromResult(pending);
    }

    public Task<IReadOnlyList<MemberReport>> GetPendingByMemberAsync(Guid reportedMemberId, CancellationToken ct = default)
    {
        IReadOnlyList<MemberReport> pending = Items
            .Where(r => r.ReportedMemberId == reportedMemberId && r.IsPending)
            .ToList();
        return Task.FromResult(pending);
    }
}
