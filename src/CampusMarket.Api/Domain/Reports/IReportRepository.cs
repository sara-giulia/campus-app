namespace CampusMarket.Api.Domain.Reports;

public interface IReportRepository
{
    /// <summary>
    /// Adds a report. Returns false if a report with the same Id already exists
    /// (nothing is added). The endpoint maps false to 409 Conflict.
    /// </summary>
    Task<bool> AddAsync(Report report, CancellationToken ct = default);

    /// <summary>
    /// True if this member has already reported this listing.
    /// </summary>
    Task<bool> ExistsAsync(Guid reporterId, Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all reports, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// The pending reports of this listing, tracked so that confirming or dismissing them
    /// is saved by the next CommitAsync. Empty if there are none.
    /// </summary>
    Task<IReadOnlyList<Report>> GetPendingByListingAsync(Guid listingId, CancellationToken ct = default);
}
