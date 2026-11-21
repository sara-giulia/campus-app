using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.UpdateMyProfile;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.UpdateMyProfile;

public class UpdateMyProfileHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_StoredMember_When_UpdatingWithValidName_Then_ChangesOnlyTheNameAndSavesOnce()
    {
        var member = Member.Register("Ada Lovelace", "ada@ubb.ro", new FakePasswordHasher().Hash("secret12"), Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMyProfileHandler(repository, unitOfWork);
        var command = new UpdateMyProfileCommand(member.Id, "  Ada King  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(member.Id);
        repository.Items[member.Id].Name.Should().Be("Ada King");
        repository.Items[member.Id].Email.Value.Should().Be("ada@ubb.ro");
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_StoredMember_When_UpdatingWithOneCharacterName_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var member = Member.Register("Ada Lovelace", "ada@ubb.ro", new FakePasswordHasher().Hash("secret12"), Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMyProfileHandler(repository, unitOfWork);
        var command = new UpdateMyProfileCommand(member.Id, "A");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.InvalidName");
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
        repository.Items[member.Id].Name.Should().Be("Ada Lovelace");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownId_When_Updating_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateMyProfileHandler(repository, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new UpdateMyProfileCommand(unknownId, "Ada King");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
