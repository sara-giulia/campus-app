using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Infrastructure;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
}
