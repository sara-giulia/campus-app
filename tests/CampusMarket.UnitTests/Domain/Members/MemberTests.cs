using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Domain.Members;

public class MemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_ValidData_When_Creating_Then_ReturnsMemberWithTrimmedNameAndNormalizedEmail()
    {
        var result = Member.Create("  Ada  ", "  Ada@UBB.RO  ", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.Name.Should().Be("Ada");
        result.Value.Email.Value.Should().Be("ada@ubb.ro");
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public void Given_InvalidName_When_Creating_Then_ReturnsValidationError(string? name)
    {
        var result = Member.Create(name!, "ada@ubb.ro", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.InvalidName");
    }

    [Fact]
    public void Given_NameLongerThanMax_When_Creating_Then_ReturnsValidationError()
    {
        var longName = new string('a', 101);

        var result = Member.Create(longName, "ada@ubb.ro", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.InvalidName");
    }

    [Fact]
    public void Given_InvalidEmail_When_Creating_Then_ReturnsValidationError()
    {
        var result = Member.Create("Ada", "not-an-email", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.Invalid");
    }

    [Fact]
    public void Given_ExistingMember_When_UpdatingWithValidData_Then_ChangesNameAndEmail()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;

        var result = member.Update("  Bob  ", "  Bob@UBB.RO  ");

        result.IsSuccess.Should().BeTrue();
        member.Name.Should().Be("Bob");
        member.Email.Value.Should().Be("bob@ubb.ro");
    }

    [Fact]
    public void Given_ExistingMember_When_UpdatingWithInvalidName_Then_ReturnsValidationError()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;

        var result = member.Update(" ", "ada@ubb.ro");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.InvalidName");
        member.Name.Should().Be("Ada");
    }

    [Fact]
    public void Given_ExistingMember_When_UpdatingWithInvalidEmail_Then_ReturnsValidationError()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;

        var result = member.Update("Ada", "not-an-email");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.Invalid");
        member.Email.Value.Should().Be("ada@ubb.ro");
    }
}
