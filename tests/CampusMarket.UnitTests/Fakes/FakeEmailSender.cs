using CampusMarket.Api.Common.Email;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeEmailSender : IEmailSender
{
    public List<SentEmail> Items { get; } = new();

    // When set, SendAsync throws it and records nothing.
    public Exception? ThrowOnSend { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        Items.Add(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    public sealed record SentEmail(string To, string Subject, string Body);
}
