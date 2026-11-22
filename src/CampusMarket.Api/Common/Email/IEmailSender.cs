namespace CampusMarket.Api.Common.Email;

// Sends a transactional email. The implementation lives in Infrastructure/Email.
// Two implementations are registered: SmtpEmailSender when Email:Host is set,
// LoggingEmailSender otherwise, so the API runs without SMTP.
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
