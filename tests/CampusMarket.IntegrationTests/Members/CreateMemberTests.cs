using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.CreateMember;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class CreateMemberTests : IntegrationTestBase
{
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidMember_When_CreatingMember_Then_Returns201AndSavesMember()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new CreateMemberRequest(Name: "  Ana Popescu  ", Email: "Ana@Student.ro");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/members", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateMemberResponse>(Json);
        body!.Name.Should().Be("Ana Popescu");
        body.Email.Should().Be("ana@student.ro");
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/members/{body.Id}");

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.Name.Should().Be("Ana Popescu");
        saved.Email.Value.Should().Be("ana@student.ro");
    }

    [Fact]
    public async Task Given_EmailWithoutAt_When_CreatingMember_Then_Returns400AndSavesNothing()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new CreateMemberRequest("Ana Popescu", "ana.student.ro");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/members", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Email.Invalid");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_CreatingMember_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var request = new CreateMemberRequest("Ana Popescu", "ana@student.ro");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/members", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_Student_When_CreatingMember_Then_Returns403AndSavesNothing()
    {
        // Arrange
        AuthenticateAs(OtherStudentId);
        var request = new CreateMemberRequest("Ana Popescu", "ana@student.ro");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/members", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Code.Should().Be("Auth.Forbidden");
        problem.Instance.Should().Be("/api/v1/members");
        problem.TraceId.Should().NotBeEmpty();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmailAlreadyUsedWithDifferentCasing_When_CreatingMember_Then_Returns409AndKeepsOneMember()
    {
        // Arrange
        var existing = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(existing);
        AuthenticateAs(AdminId, "Admin");
        var request = new CreateMemberRequest("Ana Ionescu", "ANA@Student.RO");

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/members", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.EmailTaken");
        error.Type.Should().Be(ErrorType.Conflict);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(existing);
    }
}
