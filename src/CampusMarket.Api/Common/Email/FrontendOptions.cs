namespace CampusMarket.Api.Common.Email;

// Bound from the "Frontend" configuration section. The password-reset email
// embeds BaseUrl in the clickable link: {BaseUrl}/reset-password?token=...
// Default points at the dev frontend on localhost:5173.
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; init; } = "http://localhost:5173";
}
