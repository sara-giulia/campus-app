using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeSaleRecordRepository : ISaleRecordRepository
{
    public List<SaleRecord> Items { get; } = new();

    public void Add(SaleRecord record) => Items.Add(record);

    public Task<List<SaleRecord>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(Items.ToList());
}
