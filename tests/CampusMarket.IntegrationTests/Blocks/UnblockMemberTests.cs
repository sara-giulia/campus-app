using System.Net;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Blocks;

public sealed class UnblockMemberTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _third = NewMember("Maria Ionescu", "maria@student.ro");

    private string BlockUrl(Guid id) => $"/api/v1/members/{id}/block";

    [Fact]
    public async Task Given_Block_When_Unblocking_Then_Returns204AndRemovesOnlyThatBlock()
    {
        // Arrange
        var block = MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value;
        var sameBlockerOtherMember = MemberBlock.Create(_seller.Id, _third.Id, CreatedAt).Value;
        var reverse = MemberBlock.Create(_buyer.Id, _seller.Id, CreatedAt).Value;
        await SeedAsync(_seller, _buyer, _third, block, sameBlockerOtherMember, reverse);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.DeleteAsync(BlockUrl(_buyer.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var left = await QueryDbAsync(db => db.Set<MemberBlock>().AsNoTracking().Select(b => b.Id).ToListAsync());
        left.Should().BeEquivalentTo([sameBlockerOtherMember.Id, reverse.Id]);
    }

    [Fact]
    public async Task Given_NoBlock_When_Unblocking_Then_Returns204AndChangesNothing()
    {
        // Arrange
        await SeedAsync(_seller, _buyer);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.DeleteAsync(BlockUrl(_buyer.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await QueryDbAsync(db => db.Set<MemberBlock>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Unblocking_Then_Returns204()
    {
        // Arrange
        await SeedAsync(_seller);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.DeleteAsync(BlockUrl(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Given_NoToken_When_Unblocking_Then_Returns401AndKeepsTheBlock()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);

        // Act
        var response = await Client.DeleteAsync(BlockUrl(_buyer.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await QueryDbAsync(db => db.Set<MemberBlock>().CountAsync())).Should().Be(1);
    }
}
