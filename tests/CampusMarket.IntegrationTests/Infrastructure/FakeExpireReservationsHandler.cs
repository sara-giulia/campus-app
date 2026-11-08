using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Listings.ExpireReservations;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Stands in for the expiry handler: fails, throws or waits until cancelled, and counts its calls. It never touches the database.
public sealed class FakeExpireReservationsHandler(FakeExpireReservationsHandler.Behaviour behaviour)
    : ICommandHandler<ExpireReservationsCommand, int>
{
    public enum Behaviour
    {
        ReturnFailure,
        Throw,
        WaitUntilCancelled
    }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public TaskCompletionSource FirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<Result<int>> HandleAsync(ExpireReservationsCommand command, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        FirstCall.TrySetResult();

        switch (behaviour)
        {
            case Behaviour.ReturnFailure:
                return Result<int>.Failure(Error.Validation("Test.Failure", "Run failed on purpose."));
            case Behaviour.Throw:
                throw new InvalidOperationException("Run failed on purpose.");
            default:
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
        }
    }
}
