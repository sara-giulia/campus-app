using System.Net;
using System.Text.Json;
using CampusMarket.Api.Infrastructure.Mailing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Infrastructure.Mailing;

// These tests change the process environment variable Email__ApiKey, so their
// collection never runs in parallel with any other test.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrevoEnvironmentCollection
{
    public const string Name = "BrevoEnvironment";
}

[Collection(BrevoEnvironmentCollection.Name)]
public sealed class BrevoEmailSenderTests : IDisposable
{
    private const string ApiKeyVariable = "Email__ApiKey";
    private const string ApiKey = "brevo-test-api-key";
    private const string From = "noreply@campusmarket.local";
    private const string To = "ana@student.ro";
    private const string Subject = "Your CampusMarket password was changed";
    private const string Body = "Hello Ana, your password was changed.";

    private readonly string? _savedApiKey;

    public BrevoEmailSenderTests()
    {
        _savedApiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        Environment.SetEnvironmentVariable(ApiKeyVariable, ApiKey);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(ApiKeyVariable, _savedApiKey);

    [Fact]
    public async Task Given_ValidApiKey_When_Sending_Then_PostsToBrevoWithApiKeyHeaderAndExpectedJsonBody()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var sender = SenderWith(handler);

        // Act
        await sender.SendAsync(To, Subject, Body, CancellationToken.None);

        // Assert
        handler.Request.Should().NotBeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be(new Uri("https://api.brevo.com/v3/smtp/email"));
        handler.Request.Headers.GetValues("api-key").Should().ContainSingle().Which.Should().Be(ApiKey);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var root = document.RootElement;
        root.GetProperty("sender").GetProperty("email").GetString().Should().Be(From);
        root.GetProperty("sender").GetProperty("name").GetString().Should().Be("CampusMarket");
        var recipients = root.GetProperty("to").EnumerateArray().ToList();
        recipients.Should().ContainSingle()
            .Which.GetProperty("email").GetString().Should().Be(To);
        root.GetProperty("subject").GetString().Should().Be(Subject);
        root.GetProperty("textContent").GetString().Should().Be(Body);
    }

    [Fact]
    public async Task Given_NonSuccessStatus_When_Sending_Then_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized);
        var sender = SenderWith(handler);

        // Act
        Func<Task> act = () => sender.SendAsync(To, Subject, Body, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*401*");
    }

    [Fact]
    public async Task Given_NoApiKeyVariable_When_Sending_Then_ThrowsInvalidOperation()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ApiKeyVariable, null);
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var sender = SenderWith(handler);

        // Act
        Func<Task> act = () => sender.SendAsync(To, Subject, Body, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{ApiKeyVariable}*");
        handler.Request.Should().BeNull();
    }

    private static BrevoEmailSender SenderWith(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var options = Options.Create(new EmailOptions { From = From });
        return new BrevoEmailSender(httpClient, options, NullLogger<BrevoEmailSender>.Instance);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }
}
