using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMyProfile;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetMyProfile;

public class GetMyProfileHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_StoredStudent_When_Handling_Then_ReturnsProfileAndLeavesRepositoryUnchanged()
    {
        var member = Member.Register("Ada Lovelace", "Ada@UBB.ro", new FakePasswordHasher().Hash("secret12"), Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var handler = new GetMyProfileHandler(repository);
        var query = new GetMyProfileQuery(member.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(member.Id);
        result.Value.Name.Should().Be("Ada Lovelace");
        result.Value.Email.Should().Be("ada@ubb.ro");
        result.Value.Role.Should().Be("Student");
        result.Value.CreatedAt.Should().Be(Now);
        repository.Items.Should().HaveCount(1);
        repository.Items.Should().ContainKey(member.Id);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_StoredAdmin_When_Handling_Then_ReturnsAdminRole()
    {
        var member = Member.Register("Grace Hopper", "grace@ubb.ro", new FakePasswordHasher().Hash("secret12"), Now, MemberRole.Admin).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var handler = new GetMyProfileHandler(repository);
        var query = new GetMyProfileQuery(member.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFound()
    {
        var repository = new FakeMemberRepository();
        var handler = new GetMyProfileHandler(repository);
        var unknownId = Guid.NewGuid();
        var query = new GetMyProfileQuery(unknownId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        repository.Items.Should().BeEmpty();
    }
}
