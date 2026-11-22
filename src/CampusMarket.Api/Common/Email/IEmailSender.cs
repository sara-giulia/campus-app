namespace CampusMarket.Api.Common.Email;

// Sends a transactional email. The implementation lives in Infrastructure/Mailing.
// Three implementations are registered: BrevoEmailSender when Email__ApiKey is set,
// SmtpEmailSender when Email:Host is set, LoggingEmailSender otherwise, so the API
// runs without SMTP (Render free blocks outbound SMTP ports, so Brevo's HTTP API
// is used there).
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
