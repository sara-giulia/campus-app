using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Blocks.GetMyBlocked;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Blocks.GetMyBlocked;

public class GetMyBlockedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _me = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _ana = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _dan = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly GetMyBlockedHandler _handler;

    public GetMyBlockedHandlerTests()
    {
        var members = new FakeMemberRepository();
        foreach (var member in new[] { _me, _ana, _dan })
            members.Items[member.Id] = member;
        _handler = new GetMyBlockedHandler(_blocks, members);
    }

    [Fact]
    public async Task Given_TwoBlocksAndSomeoneBlockingMe_When_Handling_Then_ReturnsOnlyMineNewestFirst()
    {
        _blocks.Add(MemberBlock.Create(_me.Id, _ana.Id, Now).Value);
        _blocks.Add(MemberBlock.Create(_me.Id, _dan.Id, Now.AddMinutes(5)).Value);
        _blocks.Add(MemberBlock.Create(_ana.Id, _me.Id, Now.AddMinutes(9)).Value);

        var result = await _handler.HandleAsync(new GetMyBlockedQuery(_me.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => (i.MemberId, i.Name, i.BlockedAt)).Should().Equal(
            (_dan.Id, "Dan Popa", Now.AddMinutes(5)),
            (_ana.Id, "Ana Pop", Now));
    }

    [Fact]
    public async Task Given_OnlyOthersBlockingMe_When_Handling_Then_ReturnsEmptyList()
    {
        _blocks.Add(MemberBlock.Create(_ana.Id, _me.Id, Now).Value);

        var result = await _handler.HandleAsync(new GetMyBlockedQuery(_me.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}
