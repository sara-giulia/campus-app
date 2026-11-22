using System.Net.Mail;
using CampusMarket.Api.Infrastructure.Mailing;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Infrastructure.Mailing;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task Given_NullHost_When_Sending_Then_Throws()
    {
        var options = Options.Create(new EmailOptions
        {
            Host = null,
            Port = 587,
            From = "noreply@campusmarket.local",
            UseSsl = true,
        });
        var sender = new SmtpEmailSender(options);

        Func<Task> act = () => sender.SendAsync("ada@student.ro", "Subject", "Body", CancellationToken.None);

        await act.Should().ThrowAsync<SmtpException>();
    }
}
