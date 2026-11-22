using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMyProfile;
using CampusMarket.Api.Features.Members.UpdateMyProfile;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class MyProfileTests : IntegrationTestBase
{
    private static readonly Guid UnknownMemberId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_LoggedInMember_When_GettingMyProfile_Then_Returns200WithProfile()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "Ana@Student.RO", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        raw.ToLowerInvariant().Should().NotContain("password");

        var body = await response.Content.ReadFromJsonAsync<GetMyProfileResponse>(Json);
        body!.Id.Should().Be(member.Id);
        body.Name.Should().Be("Ana Popescu");
        body.Email.Should().Be("ana@student.ro");
        body.Role.Should().Be("Student");
        body.CreatedAt.Should().Be(CreatedAt);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_GettingMyProfile_Then_Returns404()
    {
        // Arrange
        AuthenticateAs(UnknownMemberId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingMyProfile_Then_Returns401AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_NoToken_When_UpdatingMyProfile_Then_Returns401AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        var request = new UpdateMyProfileRequest("Ana Ionescu");

        // Act
        var response = await Client.PutAsJsonAsync("/api/v1/members/me", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_ValidName_When_UpdatingMyProfile_Then_Returns204AndSavesName()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new UpdateMyProfileRequest("Ana Ionescu");

        // Act
        var response = await Client.PutAsJsonAsync("/api/v1/members/me", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(member.Id);
        saved.Name.Should().Be("Ana Ionescu");
        saved.Email.Value.Should().Be("ana@student.ro");
    }

    [Fact]
    public async Task Given_OneCharacterName_When_UpdatingMyProfile_Then_Returns400AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new UpdateMyProfileRequest("A");

        // Act
        var response = await Client.PutAsJsonAsync("/api/v1/members/me", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.InvalidName");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_UpdatingMyProfile_Then_Returns404AndKeepsOtherMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(UnknownMemberId);
        var request = new UpdateMyProfileRequest("Ion Ionescu");

        // Act
        var response = await Client.PutAsJsonAsync("/api/v1/members/me", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }
}
