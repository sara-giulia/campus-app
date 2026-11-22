using CampusMarket.Api.Common.Email;

namespace CampusMarket.Api.Infrastructure.Mailing;

// No-SMTP fallback: writes "email to {to}: {subject}" to the log and drops
// the body on the floor so test logs never leak reset-password links. Picked
// by CampusMarketSetup when Email:Host is not set.
public sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        LogEmail(logger, to, subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "email to {To}: {Subject}")]
    private static partial void LogEmail(ILogger logger, string to, string subject);
}
