using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class LiftSuspensionTests : IntegrationTestBase
{
    private const string Reason = "Inselatorie repetata";
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static string LiftUrl(Guid memberId) => $"/api/v1/moderation/members/{memberId}/lift-suspension";

    private static Member SuspendedMember(DateTimeOffset? until = null)
    {
        var member = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        member.Suspend(AdminId, Reason, until, DateTimeOffset.UtcNow.ToMicroseconds());
        return member;
    }

    private (WebApplicationFactory<Program> Host, HttpClient Client) WithRecordedEmails(RecordingEmailSender sender)
    {
        var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(sender);
        }));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(AdminId, "Admin"));
        return (host, client);
    }

    private Task<Member> MemberAsync(Guid id) =>
        QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == id));

    [Fact]
    public async Task Given_SuspendedMember_When_AdminLiftsSuspension_Then_Returns204AndClearsTheColumnsAndSendsTheEmail()
    {
        // Arrange
        var ion = SuspendedMember();
        await SeedAsync(ion);
        var sender = new RecordingEmailSender();
        var (host, client) = WithRecordedEmails(sender);
        await using var _ = host;
        using var __ = client;

        // Act
        var response = await client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await MemberAsync(ion.Id);
        saved.SuspendedAt.Should().BeNull();
        saved.SuspendedUntil.Should().BeNull();
        saved.SuspensionReason.Should().BeNull();
        saved.SuspendedById.Should().BeNull();

        var email = sender.Sent.Should().ContainSingle().Subject;
        email.To.Should().Be("ion@student.ro");
        email.Subject.Should().Be("Contul tău CampusMarket a fost reactivat / Your CampusMarket account was reactivated");
        email.Body.Should().StartWith("Salut Ion Ionescu,");
        email.Body.Should().Contain("Suspendarea contului tău CampusMarket a fost ridicată.");
        email.Body.Should().Contain("The suspension of your CampusMarket account was lifted.");
        email.Body.Should().Contain("http://localhost:5173/my/listings");
        email.Body.IndexOf("ridicată", StringComparison.Ordinal)
            .Should().BeLessThan(email.Body.IndexOf("----------", StringComparison.Ordinal));
        email.Body.IndexOf("----------", StringComparison.Ordinal)
            .Should().BeLessThan(email.Body.IndexOf("was lifted", StringComparison.Ordinal));
        email.Body.Should().NotContain(Reason);
    }

    [Fact]
    public async Task Given_TemporarySuspension_When_AdminLiftsItEarly_Then_Returns204()
    {
        // Arrange
        var ion = SuspendedMember(DateTimeOffset.UtcNow.AddDays(3));
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var saved = await MemberAsync(ion.Id);
        saved.SuspendedAt.Should().BeNull();
        saved.SuspendedUntil.Should().BeNull();
    }

    [Fact]
    public async Task Given_EmailSenderFails_When_AdminLiftsSuspension_Then_Returns204AndTheSuspensionIsLifted()
    {
        // Arrange
        var ion = SuspendedMember();
        await SeedAsync(ion);
        var sender = new RecordingEmailSender { Throws = true };
        var (host, client) = WithRecordedEmails(sender);
        await using var _ = host;
        using var __ = client;

        // Act
        var response = await client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        sender.Attempts.Should().Be(1);
        (await MemberAsync(ion.Id)).SuspendedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_MemberNeverSuspended_When_AdminLiftsSuspension_Then_Returns409NotSuspended()
    {
        // Arrange
        var ion = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotSuspended");
        error.Type.Should().Be(ErrorType.Conflict);
        (await MemberAsync(ion.Id)).SuspendedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_TemporarySuspensionThatEnded_When_AdminLiftsSuspension_Then_Returns409AndTheColumnsStay()
    {
        // Arrange
        var ion = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var now = DateTimeOffset.UtcNow.ToMicroseconds();
        ion.Suspend(AdminId, Reason, now.AddHours(-1), now.AddHours(-2));
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotSuspended");
        var saved = await MemberAsync(ion.Id);
        saved.SuspendedAt.Should().Be(now.AddHours(-2));
        saved.SuspendedUntil.Should().Be(now.AddHours(-1));
    }

    [Fact]
    public async Task Given_UnknownMember_When_AdminLiftsSuspension_Then_Returns404()
    {
        // Arrange
        var ion = SuspendedMember();
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(LiftUrl(Guid.NewGuid()), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        (await MemberAsync(ion.Id)).SuspensionReason.Should().Be(Reason);
    }

    [Fact]
    public async Task Given_Student_When_LiftingSuspension_Then_Returns403()
    {
        // Arrange
        var ion = SuspendedMember();
        await SeedAsync(ion);
        AuthenticateAs(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        // Act
        var response = await Client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Forbidden");
        (await MemberAsync(ion.Id)).SuspensionReason.Should().Be(Reason);
    }

    [Fact]
    public async Task Given_NoToken_When_LiftingSuspension_Then_Returns401()
    {
        // Arrange
        var ion = SuspendedMember();
        await SeedAsync(ion);

        // Act
        var response = await Client.PostAsync(LiftUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Unauthorized");
        (await MemberAsync(ion.Id)).SuspensionReason.Should().Be(Reason);
    }
}
