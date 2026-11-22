using CampusMarket.Api.Infrastructure.Mailing;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Infrastructure;

// These tests change the process environment variables Email__Username and Email__Password,
// so their collection never runs in parallel with any other test.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SmtpEnvironmentCollection
{
    public const string Name = "SmtpEnvironment";
}

// Covers SmtpEmailSender against FakeSmtpServer on 127.0.0.1: the message reaches the server,
// and the credentials come from the environment variables only when Email__Username is set.
[Collection(SmtpEnvironmentCollection.Name)]
public sealed class SmtpEmailSenderTests : IDisposable
{
    private const string UsernameVariable = "Email__Username";
    private const string PasswordVariable = "Email__Password";
    private const string From = "noreply@campusmarket.local";
    private const string To = "ana@student.ro";
    private const string Subject = "Your CampusMarket password was changed";
    private const string Body = "Hello Ana, your password was changed.";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly string? _savedUsername;
    private readonly string? _savedPassword;

    public SmtpEmailSenderTests()
    {
        _savedUsername = Environment.GetEnvironmentVariable(UsernameVariable);
        _savedPassword = Environment.GetEnvironmentVariable(PasswordVariable);
        Environment.SetEnvironmentVariable(UsernameVariable, null);
        Environment.SetEnvironmentVariable(PasswordVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UsernameVariable, _savedUsername);
        Environment.SetEnvironmentVariable(PasswordVariable, _savedPassword);
    }

    [Fact]
    public async Task Given_NoUsernameVariable_When_Sending_Then_DeliversTheMessageWithoutAuthentication()
    {
        // Arrange
        await using var server = new FakeSmtpServer();
        var sender = new SmtpEmailSender(OptionsFor(server));

        // Act
        await sender.SendAsync(To, Subject, Body, CancellationToken.None).WaitAsync(Timeout);

        // Assert
        await server.Completion.WaitAsync(Timeout);
        server.MailFrom.Should().Contain(From);
        server.RcptTo.Should().ContainSingle().Which.Should().Contain(To);
        server.Data.Should().Contain($"Subject: {Subject}").And.Contain(Body);
        server.Username.Should().BeNull();
        server.Password.Should().BeNull();
    }

    [Fact]
    public async Task Given_UsernameAndPasswordVariables_When_Sending_Then_AuthenticatesWithThemAndDeliversTheMessage()
    {
        // Arrange
        Environment.SetEnvironmentVariable(UsernameVariable, "smtp-test-user");
        Environment.SetEnvironmentVariable(PasswordVariable, "smtp-test-password");
        await using var server = new FakeSmtpServer();
        var sender = new SmtpEmailSender(OptionsFor(server));

        // Act
        await sender.SendAsync(To, Subject, Body, CancellationToken.None).WaitAsync(Timeout);

        // Assert
        await server.Completion.WaitAsync(Timeout);
        server.Username.Should().Be("smtp-test-user");
        server.Password.Should().Be("smtp-test-password");
        server.MailFrom.Should().Contain(From);
        server.RcptTo.Should().ContainSingle().Which.Should().Contain(To);
        server.Data.Should().Contain($"Subject: {Subject}").And.Contain(Body);
    }

    // UseSsl false only because the local fake has no TLS.
    private static IOptions<EmailOptions> OptionsFor(FakeSmtpServer server) =>
        Options.Create(new EmailOptions { Host = "127.0.0.1", Port = server.Port, From = From, UseSsl = false });
}
