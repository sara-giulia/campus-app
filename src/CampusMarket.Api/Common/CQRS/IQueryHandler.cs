using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Common.CQRS;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct);
}
