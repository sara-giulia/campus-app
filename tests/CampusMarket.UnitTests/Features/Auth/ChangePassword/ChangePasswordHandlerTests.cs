using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Auth.ChangePassword;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Auth.ChangePassword;

public class ChangePasswordHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private const string CurrentPassword = "secret12";

    private static Member BuildRegisteredMember() =>
        Member.Register("Ada", "ada@ubb.ro", new FakePasswordHasher().Hash(CurrentPassword), Now).Value;

    [Fact]
    public async Task Given_RightCurrentPassword_When_Handling_Then_StoresNewHashUpdatesAndSavesOnce()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, hasher, unitOfWork);
        var command = new ChangePasswordCommand(member.Id, CurrentPassword, "newsecret1", "newsecret1");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        repository.Items[member.Id].PasswordHash.Should().Be(hasher.Hash("newsecret1"));
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_WrongCurrentPassword_When_Handling_Then_ReturnsInvalidCredentialsAndDoesNotSave()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, hasher, unitOfWork);
        var command = new ChangePasswordCommand(member.Id, "wrongpass", "newsecret1", "newsecret1");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
        member.PasswordHash.Should().Be(hasher.Hash(CurrentPassword));
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberWithoutPassword_When_Handling_Then_ReturnsInvalidCredentials()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, new FakePasswordHasher(), unitOfWork);
        var command = new ChangePasswordCommand(member.Id, CurrentPassword, "newsecret1", "newsecret1");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        result.Error.Message.Should().Be("The email or the password is wrong.");
        member.PasswordHash.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("")]
    public async Task Given_WeakNewPassword_When_Handling_Then_ReturnsWeakPasswordAndDoesNotSave(string newPassword)
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, hasher, unitOfWork);
        var command = new ChangePasswordCommand(member.Id, CurrentPassword, newPassword, newPassword);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.WeakPassword");
        result.Error.Message.Should().Be("Password must have at least 8 characters.");
        member.PasswordHash.Should().Be(hasher.Hash(CurrentPassword));
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NewPasswordOfExactlyMinLength_When_Handling_Then_Succeeds()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, hasher, unitOfWork);
        var command = new ChangePasswordCommand(member.Id, CurrentPassword, "12345678", "12345678");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        member.PasswordHash.Should().Be(hasher.Hash("12345678"));
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DifferentConfirmation_When_Handling_Then_ReturnsPasswordsDoNotMatchAndDoesNotSave()
    {
        var member = BuildRegisteredMember();
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var hasher = new FakePasswordHasher();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, hasher, unitOfWork);
        var command = new ChangePasswordCommand(member.Id, CurrentPassword, "newsecret1", "newsecret2");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Auth.PasswordsDoNotMatch");
        result.Error.Message.Should().Be("The two passwords are not the same.");
        member.PasswordHash.Should().Be(hasher.Hash(CurrentPassword));
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var memberId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(new FakeMemberRepository(), new FakePasswordHasher(), unitOfWork);
        var command = new ChangePasswordCommand(memberId, CurrentPassword, "newsecret1", "newsecret1");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{memberId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_HasherReturnsEmptyHash_When_Handling_Then_ReturnsPasswordRequiredAndDoesNotSave()
    {
        var member = BuildRegisteredMember();
        var originalHash = member.PasswordHash;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangePasswordHandler(repository, new EmptyHashPasswordHasher(), unitOfWork);
        var command = new ChangePasswordCommand(member.Id, CurrentPassword, "newsecret1", "newsecret1");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.PasswordRequired");
        result.Error.Message.Should().Be("A password is required.");
        member.PasswordHash.Should().Be(originalHash);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    private sealed class EmptyHashPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "";

        public bool Verify(string passwordHash, string password) => true;
    }
}
