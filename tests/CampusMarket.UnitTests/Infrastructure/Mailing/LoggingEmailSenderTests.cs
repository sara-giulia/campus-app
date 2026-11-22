using CampusMarket.Api.Infrastructure.Mailing;
using Microsoft.Extensions.Logging;

namespace CampusMarket.UnitTests.Infrastructure.Mailing;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task Given_Email_When_Sending_Then_LogsInformationWithToAndSubjectAndNotBody()
    {
        var logger = new CapturingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(logger);

        await sender.SendAsync("ada@student.ro", "Reset your CampusMarket password", "secret-body-token-abc", CancellationToken.None);

        logger.Entries.Should().HaveCount(1);
        var entry = logger.Entries.Single();
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("ada@student.ro");
        entry.Message.Should().Contain("Reset your CampusMarket password");
        entry.Message.Should().NotContain("secret-body-token-abc");
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }
}
