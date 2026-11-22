using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Register;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class RegisterRateLimitTests : IntegrationTestBase
{
    private const string Password = "secret12";
    private const string SixthEmail = "student6@student.ro";

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
