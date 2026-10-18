using CampusMarket.Api.Domain.Members;
using Xunit;

namespace CampusMarket.UnitTests.Domain;

public class MemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Email_Empty_ShouldFail(string? value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal("Email.Empty", result.Error.Code);
    }

    [Theory]
    [InlineData("@gmail.com")]       // nothing before @
    [InlineData("ana@")]             // nothing after @
    [InlineData("ana@b@gmail.com")]  // two @
    public void Email_WrongFormat_ShouldFail(string value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal("Email.Invalid", result.Error.Code);
    }

    [Fact]
    public void Email_WithSpaces_ShouldBeTrimmed()
    {
        var result = Email.Create("  ana@gmail.com  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("ana@gmail.com", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_EmptyName_ShouldFail(string? name)
    {
        var result = Member.Create(name!, "ana@gmail.com", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Member.InvalidName", result.Error.Code);
    }

    [Fact]
    public void Create_NameTooLong_ShouldFail()
    {
        var result = Member.Create(new string('a', 101), "ana@gmail.com", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Member.InvalidName", result.Error.Code);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void Create_NameAtTheLimits_ShouldSucceed(int length)
    {
        var result = Member.Create(new string('a', length), "ana@gmail.com", Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_NameWithSpaces_ShouldBeTrimmed()
    {
        var result = Member.Create("  Ana  ", "ana@gmail.com", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana", result.Value.Name);
    }

    [Fact]
    public void Update_ValidData_ShouldChangeNameAndEmail()
    {
        var member = Member.Create("Ana", "ana@gmail.com", Now).Value;

        var result = member.Update(" Maria ", "Maria@Gmail.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("Maria", member.Name);
        Assert.Equal("maria@gmail.com", member.Email.Value);
        Assert.Equal(Now, member.CreatedAt);
    }

    [Fact]
    public void Update_InvalidEmail_ShouldNotChangeData()
    {
        var member = Member.Create("Ana", "ana@gmail.com", Now).Value;

        var result = member.Update("Maria", "not-an-email");

        Assert.True(result.IsFailure);
        Assert.Equal("Email.Invalid", result.Error.Code);
        Assert.Equal("Ana", member.Name);
        Assert.Equal("ana@gmail.com", member.Email.Value);
    }
}

