namespace CampusMarket.UnitTests.Fakes;

// Like FixedTimeProvider, but a test can move the clock between two calls of a handler.
public sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
