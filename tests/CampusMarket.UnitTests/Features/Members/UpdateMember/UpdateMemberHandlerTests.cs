using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.UpdateMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.UpdateMember;

public class UpdateMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static Member BuildMember() => Member.Create("Ada", "ada@ubb.ro", Now).Value;

    [Fact]
    public async Task Given_ExistingMember_When_HandlingValidCommand_Then_UpdatesEntityAndSavesOnce()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, member.Id, false, "Bob", "Bob@UBB.RO");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        member.Name.Should().Be("Bob");
        member.Email.Value.Should().Be("bob@ubb.ro");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new UpdateMemberCommand(unknownId, Guid.NewGuid(), false, "Bob", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExistingMember_When_HandlingWithInvalidData_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, member.Id, false, " ", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.InvalidName");
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
        unitOfWork.SaveCount.Should().Be(0);
        member.Name.Should().Be("Ada");
    }

    [Fact]
    public async Task Given_NewEmailUsedByAnotherMember_When_Handling_Then_ReturnsEmailTakenConflictAndDoesNotSave()
    {
        var member = BuildMember();
        var other = Member.Create("Bob", "bob@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        repository.Items[other.Id] = other;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, member.Id, false, "Ada", "BOB@UBB.RO");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.EmailTaken");
        result.Error.Message.Should().Be("A member with email 'bob@ubb.ro' already exists.");
        unitOfWork.SaveCount.Should().Be(0);
        member.Name.Should().Be("Ada");
        member.Email.Value.Should().Be("ada@ubb.ro");
    }

    [Fact]
    public async Task Given_ExistingMember_When_KeepingOwnEmail_Then_UpdatesAndSavesOnce()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, member.Id, false, "Ada Lovelace", "ADA@UBB.RO");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        member.Name.Should().Be("Ada Lovelace");
        member.Email.Value.Should().Be("ada@ubb.ro");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ExistingMember_When_HandlingWithInvalidEmail_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, member.Id, false, "Ada", "not-an-email");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Email.Invalid");
        result.Error.Message.Should().Be("Email must contain one @ with text before and after it.");
        unitOfWork.SaveCount.Should().Be(0);
        member.Name.Should().Be("Ada");
        member.Email.Value.Should().Be("ada@ubb.ro");
    }

    [Fact]
    public async Task Given_ProfileOfAnotherMember_When_NonAdminUpdates_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, OtherMemberId, false, "Bob", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Member.NotOwner");
        result.Error.Message.Should().Be("Only the member or an Admin can change this profile.");
        repository.Items.Should().ContainKey(member.Id);
        member.Name.Should().Be("Ada");
        member.Email.Value.Should().Be("ada@ubb.ro");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ProfileOfAnotherMember_When_AdminUpdates_Then_UpdatesEntityAndSavesOnce()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, AdminId, true, "Bob", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        member.Name.Should().Be("Bob");
        member.Email.Value.Should().Be("bob@ubb.ro");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_NonOwnerNonAdminUpdates_Then_ReturnsNotFoundNotForbidden()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new UpdateMemberCommand(unknownId, OtherMemberId, false, "Bob", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ProfileOfAnotherMember_When_NonAdminSendsEmailOfThirdMember_Then_ReturnsNotOwnerNotEmailTaken()
    {
        var member = BuildMember();
        var third = Member.Create("Carol", "carol@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        repository.Items[third.Id] = third;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, OtherMemberId, false, "Ada", "carol@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Member.NotOwner");
        result.Error.Message.Should().Be("Only the member or an Admin can change this profile.");
        member.Email.Value.Should().Be("ada@ubb.ro");
        unitOfWork.SaveCount.Should().Be(0);
    }
}
