using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;

namespace CampusMarket.UnitTests.Domain.Blocks;

public class MemberBlockTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BlockerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BlockedId = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Given_TwoMembers_When_Creating_Then_SetsAllFields()
    {
        var result = MemberBlock.Create(BlockerId, BlockedId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.BlockerId.Should().Be(BlockerId);
        result.Value.BlockedId.Should().Be(BlockedId);
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Given_SameMemberTwice_When_Creating_Then_FailsWithBlockSelf()
    {
        var result = MemberBlock.Create(BlockerId, BlockerId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Block.Self");
        result.Error.Message.Should().Be("You cannot block yourself.");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Given_Nothing_When_BuildingBlockedError_Then_IsForbiddenMemberBlocked()
    {
        var error = MemberBlock.BlockedError();

        error.Code.Should().Be("Member.Blocked");
        error.Type.Should().Be(ErrorType.Forbidden);
        error.Message.Should().Be("This member has blocked you.");
    }
}
