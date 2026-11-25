using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Blocks.GetMyBlocked;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Blocks;

public sealed class GetMyBlockedTests : IntegrationTestBase
{
    private const string Url = "/api/v1/members/me/blocked";

    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _third = NewMember("Maria Ionescu", "maria@student.ro");

    [Fact]
    public async Task Given_TwoBlocksAndOneAgainstMe_When_GettingMyBlocked_Then_Returns200WithMineNewestFirst()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _third,
            MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value,
            MemberBlock.Create(_seller.Id, _third.Id, CreatedAt.AddMinutes(5)).Value,
            MemberBlock.Create(_buyer.Id, _seller.Id, CreatedAt.AddMinutes(9)).Value);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyBlockedResponse>(Json);
        body!.Items.Select(i => (i.MemberId, i.Name, i.BlockedAt.ToMicroseconds())).Should().Equal(
            (_third.Id, "Maria Ionescu", CreatedAt.AddMinutes(5).ToMicroseconds()),
            (_buyer.Id, "Ana Popescu", CreatedAt.ToMicroseconds()));
    }

    [Fact]
    public async Task Given_BlockedMember_When_GettingTheirOwnList_Then_Returns200Empty()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyBlockedResponse>(Json);
        body!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NoToken_When_GettingMyBlocked_Then_Returns401()
    {
        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
