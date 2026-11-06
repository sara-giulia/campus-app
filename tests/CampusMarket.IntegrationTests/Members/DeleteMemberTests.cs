using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class DeleteMemberTests : IntegrationTestBase
{
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ExistingMember_When_DeletingMember_Then_Returns204AndRemovesMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var exists = await QueryDbAsync(db => db.Set<Member>().AnyAsync(m => m.Id == member.Id));
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Given_UnknownId_When_DeletingMember_Then_Returns404()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_NoToken_When_DeletingMember_Then_Returns401AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/members/{member.Id}");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_AnotherMember_When_DeletingMember_Then_Returns403AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_Admin_When_DeletingMember_Then_Returns204AndRemovesMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.DeleteAsync($"/api/v1/members/{member.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var exists = await QueryDbAsync(db => db.Set<Member>().AnyAsync(m => m.Id == member.Id));
        exists.Should().BeFalse();
    }
}
