namespace CampusMarket.IntegrationTests.Infrastructure;

// PostgreSQL stores timestamps with microsecond precision, while DateTimeOffset has 100 ns ticks.
// A value saved and read back equals the original rounded down to a whole microsecond.
public static class PostgresTime
{
    public static DateTimeOffset ToMicroseconds(this DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % 10));

    public static DateTimeOffset? ToMicroseconds(this DateTimeOffset? value) =>
        value?.ToMicroseconds();
}
