using CampusMarket.Api.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfSaleRecordRepository(CampusMarketDbContext db) : ISaleRecordRepository
{
    public void Add(SaleRecord record) => db.SaleRecords.Add(record);

    public async Task<List<SaleRecord>> GetAllAsync(CancellationToken ct = default) =>
        await db.SaleRecords.AsNoTracking().ToListAsync(ct);
}
