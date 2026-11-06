using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeReportRepository : IReportRepository
{
    public List<Report> Items { get; } = new();

    public bool RejectAdd { get; set; }

    public Task<bool> AddAsync(Report report, CancellationToken ct = default)
    {
        if (RejectAdd)
            return Task.FromResult(false);

        if (Items.Any(r => r.Id == report.Id))
            return Task.FromResult(false);

        Items.Add(report);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(Guid reporterId, Guid listingId, CancellationToken ct = default)
    {
        var exists = Items.Any(r => r.ReporterId == reporterId && r.ListingId == listingId);
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Report> snapshot = Items.ToList();
        return Task.FromResult(snapshot);
    }
}
