using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Keeps every log entry written by the app in a thread-safe list, so a test can read the logs.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose() { }

    public sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger(string category, ConcurrentQueue<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception));
    }
}
