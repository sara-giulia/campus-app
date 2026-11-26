using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Features.Blocks.UnblockMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Blocks.UnblockMember;

public class UnblockMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BlockerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BlockedId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdId = new("33333333-3333-3333-3333-333333333333");

    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UnblockMemberHandler _handler;

    public UnblockMemberHandlerTests()
    {
        _handler = new UnblockMemberHandler(_blocks, _unitOfWork);
    }

    private MemberBlock Block(Guid blocker, Guid blocked)
    {
        var block = MemberBlock.Create(blocker, blocked, Now).Value;
        _blocks.Add(block);
        return block;
    }

    [Fact]
    public async Task Given_Block_When_Unblocking_Then_RemovesOnlyThatBlockAndSavesOnce()
    {
        Block(BlockerId, BlockedId);
        var sameBlockerOtherMember = Block(BlockerId, ThirdId);
        var reverse = Block(BlockedId, BlockerId);

        var result = await _handler.HandleAsync(new UnblockMemberCommand(BlockerId, BlockedId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(BlockedId);
        _blocks.Items.Should().BeEquivalentTo([sameBlockerOtherMember, reverse]);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_NoBlock_When_Unblocking_Then_SucceedsWithoutSaving()
    {
        var result = await _handler.HandleAsync(new UnblockMemberCommand(BlockerId, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _blocks.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }
}
