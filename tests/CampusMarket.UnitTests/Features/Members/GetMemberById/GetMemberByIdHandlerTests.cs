using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMemberById;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetMemberById;

public class GetMemberByIdHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ExistingMember_When_Handling_Then_ReturnsResponseMatchingEntity()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;
        var repository = new FakeMemberRepository();
        repository.Items[member.Id] = member;
        var handler = new GetMemberByIdHandler(repository);
        var query = new GetMemberByIdQuery(member.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(member.Id);
        result.Value.Name.Should().Be(member.Name);
        result.Value.Email.Should().Be(member.Email.Value);
        result.Value.CreatedAt.Should().Be(member.CreatedAt);
        repository.Items.Should().ContainKey(member.Id);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFound()
    {
        var repository = new FakeMemberRepository();
        var handler = new GetMemberByIdHandler(repository);
        var unknownId = Guid.NewGuid();
        var query = new GetMemberByIdQuery(unknownId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
    }
}
