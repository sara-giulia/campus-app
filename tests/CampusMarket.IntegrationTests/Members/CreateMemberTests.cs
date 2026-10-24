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
    [Fact]
    public async Task Given_ValidMember_When_CreatingMember_Then_Returns201AndSavesMember()
    {
        // Arrange
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
}
