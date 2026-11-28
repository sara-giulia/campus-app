using CampusMarket.Api.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfMemberReportRepository(CampusMarketDbContext db) : IMemberReportRepository
{
    public void Add(MemberReport report) => db.MemberReports.Add(report);

    public Task<bool> HasPendingAsync(Guid reporterId, Guid reportedMemberId, CancellationToken ct = default) =>
        db.MemberReports.AnyAsync(r => r.ReporterId == reporterId
                                       && r.ReportedMemberId == reportedMemberId
                                       && r.Resolution == ReportResolution.Pending, ct);

    public async Task<IReadOnlyList<MemberReport>> GetAllPendingAsync(CancellationToken ct = default) =>
        await db.MemberReports
            .AsNoTracking()
            .Where(r => r.Resolution == ReportResolution.Pending)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MemberReport>> GetPendingByMemberAsync(Guid reportedMemberId, CancellationToken ct = default) =>
        await db.MemberReports
            .Where(r => r.ReportedMemberId == reportedMemberId && r.Resolution == ReportResolution.Pending)
            .ToListAsync(ct);
}
