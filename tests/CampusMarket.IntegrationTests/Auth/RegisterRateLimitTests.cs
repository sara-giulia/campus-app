using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Register;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class RegisterRateLimitTests : IntegrationTestBase
{
    private const string Password = "secret12";
    private const string SixthEmail = "student6@student.ro";
    private const string FirstIp = "10.0.0.1";
    private const string SecondIp = "10.0.0.2";

    // A copy of the API on the same in-memory database, with the real register limit.
    private WebApplicationFactory<Program> WithRealRegisterLimit() =>
        Factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:RegisterPermitLimit", "5"));

    [Fact]
    public async Task Given_FiveRegistrations_When_RegisteringASixthTime_Then_Returns429AndSavesOnlyTheFirstFive()
    {
        // Arrange
        await using var limited = WithRealRegisterLimit();
        var client = limited.CreateClient();
        var firstFive = await RegisterFiveAsync(client, Password, Password);
        var request = new RegisterRequest("Ana Popescu", SixthEmail, Password, Password);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        firstFive.Should().AllBeEquivalentTo(HttpStatusCode.Created);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(5);
        members.Should().NotContain(m => m.Email.Value == SixthEmail);
    }

    [Fact]
    public async Task Given_FiveRegistrationsWithMismatchedPasswords_When_RegisteringASixthTime_Then_Returns429AndSavesNothing()
    {
        // Arrange
        await using var limited = WithRealRegisterLimit();
        var client = limited.CreateClient();
        var firstFive = await RegisterFiveAsync(client, Password, "different12");
        var request = new RegisterRequest("Ana Popescu", SixthEmail, Password, Password);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        firstFive.Should().AllBeEquivalentTo(HttpStatusCode.BadRequest);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_TestLimitOf1000_When_RegisteringASixthTime_Then_Returns201AndSavesSixMembers()
    {
        // Arrange
        var firstFive = await RegisterFiveAsync(Client, Password, Password);
        var request = new RegisterRequest("Ana Popescu", SixthEmail, Password, Password);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        firstFive.Should().AllBeEquivalentTo(HttpStatusCode.Created);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(Json);
        body!.Email.Should().Be(SixthEmail);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(6);
        members.Should().ContainSingle(m => m.Email.Value == SixthEmail);
    }

    [Fact]
    public async Task Given_FiveRegistrationsFromOneIp_When_RegisteringFromAnotherIp_Then_Returns201AndSavesSixMembers()
    {
        // Arrange
        await using var limited = WithRealRegisterLimitAndRemoteIp();
        var client = limited.CreateClient();
        var firstFive = await RegisterFiveFromIpAsync(client, FirstIp);
        using var request = RegisterFromIp(SecondIp, new RegisterRequest("Ana Popescu", SixthEmail, Password, Password));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        firstFive.Should().AllBeEquivalentTo(HttpStatusCode.Created);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(Json);
        body!.Email.Should().Be(SixthEmail);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(6);
        members.Should().ContainSingle(m => m.Email.Value == SixthEmail);
    }

    [Fact]
    public async Task Given_FiveRegistrationsFromOneIp_When_RegisteringASixthTimeFromTheSameIp_Then_Returns429AndSavesOnlyTheFirstFive()
    {
        // Arrange
        await using var limited = WithRealRegisterLimitAndRemoteIp();
        var client = limited.CreateClient();
        var firstFive = await RegisterFiveFromIpAsync(client, FirstIp);
        using var request = RegisterFromIp(FirstIp, new RegisterRequest("Ana Popescu", SixthEmail, Password, Password));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        firstFive.Should().AllBeEquivalentTo(HttpStatusCode.Created);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(5);
        members.Should().NotContain(m => m.Email.Value == SixthEmail);
    }

    // The same copy as WithRealRegisterLimit, which also reads the remote IP from the X-Test-Remote-Ip header.
    private WebApplicationFactory<Program> WithRealRegisterLimitAndRemoteIp() =>
        WithRealRegisterLimit().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>()));

    private static HttpRequestMessage RegisterFromIp(string ip, RegisterRequest register)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(register, options: Json)
        };
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, ip);
        return request;
    }

    // Sends five valid registrations with different emails from one IP and returns their status codes.
    private static async Task<List<HttpStatusCode>> RegisterFiveFromIpAsync(HttpClient client, string ip)
    {
        var statusCodes = new List<HttpStatusCode>();
        foreach (var n in Enumerable.Range(1, 5))
        {
            using var request = RegisterFromIp(ip, new RegisterRequest("Ana Popescu", $"student{n}@student.ro", Password, Password));
            using var response = await client.SendAsync(request);
            statusCodes.Add(response.StatusCode);
        }

        return statusCodes;
    }

    // Sends five registrations with different emails and returns their status codes.
    private static async Task<List<HttpStatusCode>> RegisterFiveAsync(HttpClient client, string password, string confirmPassword)
    {
        var responses = new List<HttpResponseMessage>
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Ana Popescu", "student1@student.ro", password, confirmPassword), Json),
            await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Ana Popescu", "student2@student.ro", password, confirmPassword), Json),
            await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Ana Popescu", "student3@student.ro", password, confirmPassword), Json),
            await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Ana Popescu", "student4@student.ro", password, confirmPassword), Json),
            await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Ana Popescu", "student5@student.ro", password, confirmPassword), Json)
        };

        return responses.Select(r => r.StatusCode).ToList();
    }
}
