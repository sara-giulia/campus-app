namespace CampusMarket.UnitTests.Fakes;

// Timestamps move only when a test calls Advance, so measured durations are exact.
public sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
}
