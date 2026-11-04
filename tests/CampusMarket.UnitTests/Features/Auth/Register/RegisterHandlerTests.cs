using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.Register;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.Register;

public class RegisterHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidCommand_When_Handling_Then_StoresMemberAndSavesOnce()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var handler = new RegisterHandler(repository, unitOfWork, hasher, new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "Ada@UBB.RO", "secret12", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Ada");
        result.Value.Email.Should().Be("ada@ubb.ro");
        result.Value.Role.Should().Be(MemberRole.Student);
        result.Value.CreatedAt.Should().Be(Now);
        repository.Items.Should().HaveCount(1);
        var stored = repository.Items[result.Value.Id];
        stored.PasswordHash.Should().Be("hashed:secret12");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")]
    public async Task Given_WeakPassword_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave(string password)
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "ada@ubb.ro", password, password);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.WeakPassword");
        result.Error.Message.Should().Be("Password must have at least 8 characters.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InvalidName_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand(" ", "ada@ubb.ro", "secret12", "secret12");

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
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "not-an-email", "secret12", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Email.Invalid");
        result.Error.Message.Should().Be("Email must contain one @ with text before and after it.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmailAlreadyUsedByAnotherMember_When_Handling_Then_ReturnsEmailTakenConflictAndDoesNotStoreOrSave()
    {
        var existing = Member.Create("Bob", "ada@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[existing.Id] = existing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "ADA@UBB.RO", "secret12", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.EmailTaken");
        result.Error.Message.Should().Be("A member with email 'ada@ubb.ro' already exists.");
        repository.Items.Should().HaveCount(1);
        repository.Items[existing.Id].Should().BeSameAs(existing);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsConflictAndDoesNotSave()
    {
        var repository = new FakeMemberRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "ada@ubb.ro", "secret12", "secret12");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.Duplicate");
        result.Error.Message.Should().Match("A member with id '*' already exists.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData("secret123", "Secret123")]
    [InlineData("secret12", "secret13")]
    public async Task Given_ConfirmPasswordDifferentFromPassword_When_Handling_Then_ReturnsValidationErrorAndDoesNotStoreOrSave(string password, string confirmPassword)
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "ada@ubb.ro", password, confirmPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.PasswordsDoNotMatch");
        result.Error.Message.Should().Be("The two passwords are not the same.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_WeakPasswordWithDifferentConfirmation_When_Handling_Then_ReturnsWeakPasswordErrorAndDoesNotStoreOrSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RegisterHandler(repository, unitOfWork, new FakePasswordHasher(), new FixedTimeProvider(Now));
        var command = new RegisterCommand("Ada", "ada@ubb.ro", "short", "different");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.WeakPassword");
        result.Error.Message.Should().Be("Password must have at least 8 characters.");
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
