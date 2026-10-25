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
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidChanges_When_UpdatingMember_Then_Returns204AndSavesChanges()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
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
}
