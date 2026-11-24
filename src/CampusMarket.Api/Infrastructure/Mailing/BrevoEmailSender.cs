using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CampusMarket.Api.Common.Email;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Mailing;

// Brevo transactional-email HTTP API sender. Used on hosts (like Render free)
// where outbound SMTP ports are blocked, so SmtpEmailSender cannot reach Brevo.
// The API key is read only from the Email__ApiKey environment variable, so it
// never appears in a committed appsettings file. Registered by CampusMarketSetup
// when Email__ApiKey is set; the SMTP sender takes over when Email:Host is set
// and the API key is not.
public sealed partial class BrevoEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private const string ApiKeyEnvVar = "Email__ApiKey";
    private const string SendEndpoint = "v3/smtp/email";
    private const string SenderName = "CampusMarket";
    private const string ApiKeyHeader = "api-key";

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException($"{ApiKeyEnvVar} environment variable is not set.");

        var payload = new BrevoEmailRequest(
            new BrevoAddress(options.Value.From, SenderName),
            new List<BrevoRecipient> { new(to) },
            subject,
            body);

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add(ApiKeyHeader, apiKey);

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            LogFailure(logger, statusCode);
            throw new HttpRequestException($"Brevo rejected the email with status {statusCode}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Brevo email request failed with status {StatusCode}")]
    private static partial void LogFailure(ILogger logger, int statusCode);

    private sealed record BrevoEmailRequest(
        [property: JsonPropertyName("sender")] BrevoAddress Sender,
        [property: JsonPropertyName("to")] List<BrevoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("textContent")] string TextContent);

    private sealed record BrevoAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);

    private sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email);
}
