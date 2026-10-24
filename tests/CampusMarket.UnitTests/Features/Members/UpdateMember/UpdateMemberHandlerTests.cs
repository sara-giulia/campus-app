using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.UpdateMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.UpdateMember;

public class UpdateMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Member BuildMember() => Member.Create("Ada", "ada@ubb.ro", Now).Value;

    [Fact]
    public async Task Given_ExistingMember_When_HandlingValidCommand_Then_UpdatesEntityAndSavesOnce()
    {
        var member = BuildMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMemberHandler(repository, unitOfWork);
        var command = new UpdateMemberCommand(member.Id, "Bob", "Bob@UBB.RO");

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
        var command = new UpdateMemberCommand(Guid.NewGuid(), "Bob", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
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
        var command = new UpdateMemberCommand(member.Id, " ", "bob@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.InvalidName");
        unitOfWork.SaveCount.Should().Be(0);
        member.Name.Should().Be("Ada");
    }
}
