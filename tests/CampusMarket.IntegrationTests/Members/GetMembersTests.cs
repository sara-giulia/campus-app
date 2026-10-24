using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMembers;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class GetMembersTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_TwoMembers_When_GettingMembers_Then_Returns200WithBothMembers()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var ion = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(ana, ion);

        // Act
        var response = await Client.GetAsync("/api/v1/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMembersResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { ana.Id, ion.Id });

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_EmptyDatabase_When_GettingMembers_Then_Returns200WithNoMembers()
    {
        // Arrange: the database starts empty

        // Act
        var response = await Client.GetAsync("/api/v1/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMembersResponse>(Json);
        body!.Count.Should().Be(0);
        body.Items.Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        count.Should().Be(0);
    }
}
