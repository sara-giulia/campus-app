using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMembers;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetMembers;

public class GetMembersHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_EmptyRepository_When_Handling_Then_ReturnsEmptyList()
    {
        var repository = new FakeMemberRepository();
        var handler = new GetMembersHandler(repository);
        var query = new GetMembersQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_RepositoryWithTwoMembers_When_Handling_Then_ReturnsThemAndLeavesRepositoryUnchanged()
    {
        var first = Member.Create("Ada", "ada@ubb.ro", Now).Value;
        var second = Member.Create("Bob", "bob@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var handler = new GetMembersHandler(repository);
        var query = new GetMembersQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        repository.Items.Should().HaveCount(2);
    }
}
