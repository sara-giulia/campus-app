using System.Collections.Concurrent;
using CampusMarket.Api.Common.Email;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Replaces the email service in a test host: remembers every email, and can fail like an unreachable SMTP server.
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();
    private int _attempts;

    public bool Throws { get; init; }

    public IReadOnlyCollection<SentEmail> Sent => _sent.ToArray();

    public int Attempts => Volatile.Read(ref _attempts);

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _attempts);
        if (Throws)
            throw new InvalidOperationException("secret: the SMTP server is down");

        _sent.Enqueue(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    public sealed record SentEmail(string To, string Subject, string Body);
}
