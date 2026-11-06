using CampusMarket.Api.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfReportRepository(CampusMarketDbContext db) : IReportRepository
{
    public async Task<bool> AddAsync(Report report, CancellationToken ct = default)
    {
        if (await db.Reports.AnyAsync(r => r.Id == report.Id, ct))
            return false;

        db.Reports.Add(report);
        return true;
    }

    public Task<bool> ExistsAsync(Guid reporterId, Guid listingId, CancellationToken ct = default) =>
        db.Reports.AnyAsync(r => r.ReporterId == reporterId && r.ListingId == listingId, ct);

    public async Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default) =>
        await db.Reports.AsNoTracking().ToListAsync(ct);
}
