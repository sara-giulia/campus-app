namespace CampusMarket.Api.Infrastructure.Mailing;

// Bound from the "Email" configuration section. Username and password are NOT on
// this class on purpose: they must only come from the Email__Username and
// Email__Password environment variables, never from appsettings.*.json. The
// SmtpEmailSender reads them directly from the environment.
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string From { get; init; } = "noreply@campusmarket.local";
    public bool UseSsl { get; init; } = true;
}
