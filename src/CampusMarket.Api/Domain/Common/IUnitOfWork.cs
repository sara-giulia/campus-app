namespace CampusMarket.Api.Domain.Common;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct);
}
