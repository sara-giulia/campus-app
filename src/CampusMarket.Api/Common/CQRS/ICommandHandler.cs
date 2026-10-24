using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Common.CQRS;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken ct);
}
