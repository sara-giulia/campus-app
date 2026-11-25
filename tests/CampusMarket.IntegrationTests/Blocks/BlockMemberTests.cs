using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Blocks;

public sealed class BlockMemberTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");

    private string BlockUrl(Guid id) => $"/api/v1/members/{id}/block";

    [Fact]
    public async Task Given_TwoMembers_When_SellerBlocksBuyer_Then_Returns204AndSavesOneBlock()
    {
        // Arrange
        await SeedAsync(_seller, _buyer);
        AuthenticateAs(_seller.Id);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await Client.PostAsync(BlockUrl(_buyer.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        var block = await QueryDbAsync(db => db.Set<MemberBlock>().AsNoTracking().SingleAsync());
        block.BlockerId.Should().Be(_seller.Id);
        block.BlockedId.Should().Be(_buyer.Id);
        block.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Given_AlreadyBlocked_When_BlockingAgain_Then_Returns204AndKeepsOneBlock()
    {
        // Arrange
        await SeedAsync(_seller, _buyer);
        AuthenticateAs(_seller.Id);
        (await Client.PostAsync(BlockUrl(_buyer.Id), content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var first = await QueryDbAsync(db => db.Set<MemberBlock>().AsNoTracking().SingleAsync());

        // Act
        var response = await Client.PostAsync(BlockUrl(_buyer.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var block = await QueryDbAsync(db => db.Set<MemberBlock>().AsNoTracking().SingleAsync());
        block.Id.Should().Be(first.Id);
        block.CreatedAt.Should().Be(first.CreatedAt);
    }

    [Fact]
    public async Task Given_OwnId_When_Blocking_Then_Returns400BlockSelfAndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsync(BlockUrl(_seller.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Block.Self");
        error.Type.Should().Be(ErrorType.Validation);
        (await QueryDbAsync(db => db.Set<MemberBlock>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Blocking_Then_Returns404MemberNotFoundAndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsync(BlockUrl(Guid.NewGuid()), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        (await QueryDbAsync(db => db.Set<MemberBlock>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_Blocking_Then_Returns401AndSavesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer);

        // Act
        var response = await Client.PostAsync(BlockUrl(_buyer.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await QueryDbAsync(db => db.Set<MemberBlock>().CountAsync())).Should().Be(0);
    }
}
