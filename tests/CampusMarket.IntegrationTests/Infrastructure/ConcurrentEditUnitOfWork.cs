using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Another user changes the listing a moment before this commit,
// so EF Core's own Version check throws a real DbUpdateConcurrencyException.
public sealed class ConcurrentEditUnitOfWork(CampusMarketDbContext db) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("UPDATE \"Listings\" SET \"Version\" = \"Version\" + 1", ct);
        return await db.SaveChangesAsync(ct);
    }
}
