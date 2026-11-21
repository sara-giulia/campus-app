namespace CampusMarket.Api.Domain.Listings;

public interface ISaleRecordRepository
{
    /// <summary>
    /// Adds a sale record to the current unit of work. Saved by the next CommitAsync.
    /// </summary>
    void Add(SaleRecord record);

    /// <summary>
    /// Returns every sale record ever written, in no particular order.
    /// The caller sorts by SoldAt when it needs "newest first".
    /// </summary>
    Task<List<SaleRecord>> GetAllAsync(CancellationToken ct = default);
}
