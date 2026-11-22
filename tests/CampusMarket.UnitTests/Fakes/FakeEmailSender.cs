using CampusMarket.Api.Common.Email;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeEmailSender : IEmailSender
{
    public List<SentEmail> Items { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Items.Add(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    public sealed record SentEmail(string To, string Subject, string Body);
}
