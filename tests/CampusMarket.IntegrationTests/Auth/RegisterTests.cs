using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Register;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Auth;

public sealed class RegisterTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidBody_When_Registering_Then_Returns201AndSavesMemberWithPasswordHash()
    {
        // Arrange
        var request = new RegisterRequest(Name: "  Ana Popescu  ", Email: "Ana@Student.ro", Password: "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(Json);
        body!.Name.Should().Be("Ana Popescu");
        body.Email.Should().Be("ana@student.ro");
        body.Role.Should().Be(MemberRole.Student);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/members/{body.Id}");

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.Name.Should().Be("Ana Popescu");
        saved.Email.Value.Should().Be("ana@student.ro");
        saved.Role.Should().Be(MemberRole.Student);
        saved.PasswordHash.Should().NotBeNullOrWhiteSpace();
        saved.PasswordHash.Should().NotBe("secret12");
    }

    [Fact]
    public async Task Given_WeakPassword_When_Registering_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var request = new RegisterRequest("Ana Popescu", "ana@student.ro", "short");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Auth.WeakPassword");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmailWithoutAt_When_Registering_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var request = new RegisterRequest("Ana Popescu", "ana.student.ro", "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Email.Invalid");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_ShortName_When_Registering_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var request = new RegisterRequest("A", "ana@student.ro", "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.InvalidName");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmailAlreadyUsed_When_Registering_Then_Returns409AndKeepsOneMember()
    {
        // Arrange
        var hasher = new AspNetPasswordHasher();
        var existing = Member.Register("Bob", "ana@student.ro", hasher.Hash("other123"), CreatedAt).Value;
        await SeedAsync(existing);
        var request = new RegisterRequest("Ana Popescu", "ANA@student.ro", "secret12");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.EmailTaken");
        error.Type.Should().Be(ErrorType.Conflict);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(1);
        members[0].Id.Should().Be(existing.Id);
    }
}
