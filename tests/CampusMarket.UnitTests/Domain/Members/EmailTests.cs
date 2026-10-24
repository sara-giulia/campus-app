using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Domain.Members;

public class EmailTests
{
    [Fact]
    public void Given_ValidEmail_When_Creating_Then_ReturnsNormalizedEmail()
    {
        var result = Email.Create("  Student@UBB.RO  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("student@ubb.ro");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyEmail_When_Creating_Then_ReturnsValidationError(string? value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Email.Empty");
    }

    [Theory]
    [InlineData("student")]
    [InlineData("@ubb.ro")]
    [InlineData("student@")]
    [InlineData("student@ubb@ro")]
    public void Given_InvalidEmail_When_Creating_Then_ReturnsValidationError(string value)
    {
        var result = Email.Create(value);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Email.Invalid");
    }
}
