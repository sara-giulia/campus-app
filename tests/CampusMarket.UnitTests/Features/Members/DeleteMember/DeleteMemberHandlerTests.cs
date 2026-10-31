using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.DeleteMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.DeleteMember;

public class DeleteMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static Member BuildMember() => Member.Create("Ada", "ada@ubb.ro", Now).Value;

    [Fact]
    public async Task Given_ExistingMember_When_Handling_Then_RemovesItAndSavesOnce()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteMemberHandler(repository, unitOfWork);
        var command = new DeleteMemberCommand(member.Id, member.Id, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteMemberHandler(repository, unitOfWork);
        var command = new DeleteMemberCommand(Guid.NewGuid(), Guid.NewGuid(), false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ProfileOfAnotherMember_When_NonAdminDeletes_Then_ReturnsForbiddenAndKeepsMember()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteMemberHandler(repository, unitOfWork);
        var command = new DeleteMemberCommand(member.Id, OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Member.NotOwner");
        repository.Items.Should().ContainKey(member.Id);
        member.Name.Should().Be("Ada");
        member.Email.Value.Should().Be("ada@ubb.ro");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ProfileOfAnotherMember_When_AdminDeletes_Then_RemovesItAndSavesOnce()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteMemberHandler(repository, unitOfWork);
        var command = new DeleteMemberCommand(member.Id, AdminId, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_NonOwnerNonAdminDeletes_Then_ReturnsNotFoundNotForbidden()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteMemberHandler(repository, unitOfWork);
        var command = new DeleteMemberCommand(Guid.NewGuid(), OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        unitOfWork.SaveCount.Should().Be(0);
    }
}
