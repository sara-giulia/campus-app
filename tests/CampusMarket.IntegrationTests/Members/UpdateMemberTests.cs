using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.UpdateMember;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class UpdateMemberTests : IntegrationTestBase
{
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidChanges_When_UpdatingMember_Then_Returns204AndSavesChanges()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new UpdateMemberRequest("Ana Ionescu", "ana.ionescu@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{member.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Name.Should().Be("Ana Ionescu");
        saved.Email.Value.Should().Be("ana.ionescu@student.ro");
    }

    [Fact]
    public async Task Given_OneCharacterName_When_UpdatingMember_Then_Returns400AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(member.Id);
        var request = new UpdateMemberRequest("A", "ana@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{member.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.InvalidName");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_NoToken_When_UpdatingMember_Then_Returns401AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        var request = new UpdateMemberRequest("Ana Ionescu", "ana.ionescu@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{member.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_AnotherMember_When_UpdatingMember_Then_Returns403AndKeepsMember()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(OtherStudentId);
        var request = new UpdateMemberRequest("Ana Ionescu", "ana.ionescu@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{member.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_Admin_When_UpdatingMember_Then_Returns204AndSavesChanges()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateMemberRequest("Ana Ionescu", "ana.ionescu@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{member.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(member.Id);
        saved.Name.Should().Be("Ana Ionescu");
        saved.Email.Value.Should().Be("ana.ionescu@student.ro");
    }

    [Fact]
    public async Task Given_UnknownId_When_UpdatingMember_Then_Returns404()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        AuthenticateAs(OtherStudentId);
        var request = new UpdateMemberRequest("Ana Ionescu", "ana.ionescu@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{Guid.NewGuid()}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(member);
    }

    [Fact]
    public async Task Given_EmailUsedByAnotherMember_When_UpdatingMember_Then_Returns409AndKeepsBothMembers()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var other = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(member, other);
        AuthenticateAs(member.Id);
        var request = new UpdateMemberRequest("Ana Popescu", "ion@student.ro");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/members/{member.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.EmailTaken");
        error.Type.Should().Be(ErrorType.Conflict);

        var savedMember = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == member.Id));
        savedMember.Should().BeEquivalentTo(member);
        var savedOther = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == other.Id));
        savedOther.Should().BeEquivalentTo(other);
    }
}
