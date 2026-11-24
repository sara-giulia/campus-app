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
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
    }

    [Fact]
    public void Given_NameLongerThanMax_When_Creating_Then_ReturnsValidationError()
    {
        var longName = new string('a', 101);

        var result = Member.Create(longName, "ada@ubb.ro", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.InvalidName");
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
    }

    [Fact]
    public void Given_NameAtMinLength_When_Creating_Then_Succeeds()
    {
        var result = Member.Create("Ad", "ada@ubb.ro", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Ad");
    }

    [Fact]
    public void Given_NameAtMaxLength_When_Creating_Then_Succeeds()
    {
        var name = new string('a', 100);

        var result = Member.Create(name, "ada@ubb.ro", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(name);
    }

    [Fact]
    public void Given_InvalidEmail_When_Creating_Then_ReturnsValidationError()
    {
        var result = Member.Create("Ada", "not-an-email", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.Invalid");
        result.Error.Message.Should().Be("Email must contain one @ with text before and after it.");
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
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
        member.Name.Should().Be("Ada");
    }

    [Fact]
    public void Given_ExistingMember_When_UpdatingWithInvalidEmail_Then_ReturnsValidationError()
    {
        var member = Member.Create("Ada", "ada@ubb.ro", Now).Value;

        var result = member.Update("Ada", "not-an-email");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.Invalid");
        result.Error.Message.Should().Be("Email must contain one @ with text before and after it.");
        member.Email.Value.Should().Be("ada@ubb.ro");
    }

    // Feature: Authentication

    [Fact]
    public void Given_ValidData_When_Registering_Then_ReturnsMemberWithPasswordHashAndStudentRole()
    {
        var result = Member.Register("  Ada  ", "  Ada@UBB.RO  ", "hashed:secret", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.Name.Should().Be("Ada");
        result.Value.Email.Value.Should().Be("ada@ubb.ro");
        result.Value.PasswordHash.Should().Be("hashed:secret");
        result.Value.Role.Should().Be(MemberRole.Student);
        result.Value.IsAdmin.Should().BeFalse();
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Given_AdminRole_When_Registering_Then_ReturnsAdminMember()
    {
        var result = Member.Register("Ada", "ada@ubb.ro", "hashed:secret", Now, MemberRole.Admin);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(MemberRole.Admin);
        result.Value.IsAdmin.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyPasswordHash_When_Registering_Then_ReturnsValidationError(string? passwordHash)
    {
        var result = Member.Register("Ada", "ada@ubb.ro", passwordHash!, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.PasswordRequired");
        result.Error.Message.Should().Be("A password is required.");
    }

    [Fact]
    public void Given_InvalidName_When_Registering_Then_ReturnsValidationError()
    {
        var result = Member.Register(" ", "ada@ubb.ro", "hashed:secret", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.InvalidName");
        result.Error.Message.Should().Be("Name must have between 2 and 100 characters.");
    }

    [Fact]
    public void Given_InvalidEmail_When_Registering_Then_ReturnsValidationError()
    {
        var result = Member.Register("Ada", "not-an-email", "hashed:secret", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Email.Invalid");
        result.Error.Message.Should().Be("Email must contain one @ with text before and after it.");
    }

    [Fact]
    public void Given_RegisteredMember_When_ChangingPasswordWithHash_Then_StoresHashAndReturnsSameInstance()
    {
        var member = Member.Register("Ada", "ada@ubb.ro", "hashed:secret12", Now).Value;

        var result = member.ChangePassword("hashed:newsecret");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(member);
        member.PasswordHash.Should().Be("hashed:newsecret");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_RegisteredMember_When_ChangingPasswordWithEmptyHash_Then_ReturnsValidationErrorAndKeepsHash(string newPasswordHash)
    {
        var member = Member.Register("Ada", "ada@ubb.ro", "hashed:secret12", Now).Value;

        var result = member.ChangePassword(newPasswordHash);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Member.PasswordRequired");
        result.Error.Message.Should().Be("A password is required.");
        member.PasswordHash.Should().Be("hashed:secret12");
    }

    [Fact]
    public void Given_RegisteredMember_When_ChangingPassword_Then_StoresTheNewHash()
    {
        var member = Member.Register("Ada", "ada@student.ro", "hashed:old", Now).Value;

        member.ChangePassword("hashed:new");

        member.PasswordHash.Should().Be("hashed:new");
    }
}
