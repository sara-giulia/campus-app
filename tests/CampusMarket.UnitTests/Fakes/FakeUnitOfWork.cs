using CampusMarket.Api.Domain.Common;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> CommitAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}
