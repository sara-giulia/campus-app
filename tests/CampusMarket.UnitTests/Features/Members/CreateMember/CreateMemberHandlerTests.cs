using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.CreateMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.CreateMember;

public class CreateMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidCommand_When_Handling_Then_StoresMemberAndSavesOnce()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateMemberHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateMemberCommand("Ada", "Ada@UBB.RO");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Ada");
        result.Value.Email.Should().Be("ada@ubb.ro");
        result.Value.CreatedAt.Should().Be(Now);
        repository.Items.Should().HaveCount(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_InvalidName_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateMemberHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateMemberCommand(" ", "ada@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.InvalidName");
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InvalidEmail_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateMemberHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateMemberCommand("Ada", "not-an-email");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Email.Invalid");
        result.Error.Message.Should().Be("Email must contain one @ with text before and after it.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsConflictAndDoesNotSave()
    {
        var repository = new FakeMemberRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateMemberHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateMemberCommand("Ada", "ada@ubb.ro");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.Duplicate");
        result.Error.Message.Should().Match("A member with id '*' already exists.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmailAlreadyUsedByAnotherMember_When_Handling_Then_ReturnsEmailTakenConflictAndDoesNotStoreOrSave()
    {
        var existing = Member.Create("Ada", "ada@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[existing.Id] = existing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateMemberHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateMemberCommand("Bob", "ADA@UBB.RO");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.EmailTaken");
        result.Error.Message.Should().Be("A member with email 'ada@ubb.ro' already exists.");
        repository.Items.Should().HaveCount(1);
        repository.Items[existing.Id].Should().BeSameAs(existing);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
