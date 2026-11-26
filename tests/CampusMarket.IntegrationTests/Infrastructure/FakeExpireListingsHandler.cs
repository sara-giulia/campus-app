using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Features.Listings.ExpireListings;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Stands in for the listing expiry handler: fails, throws, expires nothing or waits until cancelled, and counts its calls. It never touches the database.
public sealed class FakeExpireListingsHandler(FakeExpireListingsHandler.Behaviour behaviour)
    : ICommandHandler<ExpireListingsCommand, int>
{
    public enum Behaviour
    {
        ReturnFailure,
        Throw,
        ReturnZero,
        WaitUntilCancelled
    }

    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public TaskCompletionSource FirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<Result<int>> HandleAsync(ExpireListingsCommand command, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        FirstCall.TrySetResult();

        switch (behaviour)
        {
            case Behaviour.ReturnFailure:
                return Result<int>.Failure(Error.Validation("Test.Failure", "Run failed on purpose."));
            case Behaviour.Throw:
                throw new InvalidOperationException("Run failed on purpose.");
            case Behaviour.ReturnZero:
                return 0;
            default:
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
        }
    }
}
