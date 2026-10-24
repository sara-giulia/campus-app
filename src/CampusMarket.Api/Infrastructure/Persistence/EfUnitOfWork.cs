using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfUnitOfWork(CampusMarketDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
