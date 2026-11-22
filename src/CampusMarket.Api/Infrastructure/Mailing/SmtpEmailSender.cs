using System.Net;
using System.Net.Mail;
using CampusMarket.Api.Common.Email;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Mailing;

// SMTP implementation using System.Net.Mail. Credentials are read from
// Email__Username and Email__Password environment variables so they never
// appear in a committed appsettings file. Registered only when Email:Host
// is set; CampusMarketSetup falls back to LoggingEmailSender otherwise.
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var o = options.Value;

        using var message = new MailMessage(o.From, to, subject, body)
        {
            IsBodyHtml = false,
        };

        using var client = new SmtpClient(o.Host!, o.Port)
        {
            EnableSsl = o.UseSsl, // NOSONAR: TLS comes from configuration (Email:UseSsl=true in production); the local Mailpit has no TLS
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        var username = Environment.GetEnvironmentVariable("Email__Username");
        var password = Environment.GetEnvironmentVariable("Email__Password");
        if (!string.IsNullOrEmpty(username))
            client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message, ct);
    }
}
