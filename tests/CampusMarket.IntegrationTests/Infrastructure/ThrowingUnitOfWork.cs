using CampusMarket.Api.Domain.Common;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Every commit fails with an exception whose message must never reach the client.
public sealed class ThrowingUnitOfWork : IUnitOfWork
{
    public Task<int> CommitAsync(CancellationToken ct) =>
        throw new InvalidOperationException("secret: Data Source=prod.db");
}
