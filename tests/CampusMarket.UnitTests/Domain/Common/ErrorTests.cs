using CampusMarket.Api.Domain.Common;

namespace CampusMarket.UnitTests.Domain.Common;

public class ErrorTests
{
    [Fact]
    public void Given_CodeAndMessage_When_CreatingValidation_Then_ReturnsValidationError()
    {
        var error = Error.Validation("Some.Code", "Some message.");

        error.Code.Should().Be("Some.Code");
        error.Message.Should().Be("Some message.");
        error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Given_CodeAndMessage_When_CreatingNotFound_Then_ReturnsNotFoundError()
    {
        var error = Error.NotFound("Some.Code", "Some message.");

        error.Code.Should().Be("Some.Code");
        error.Message.Should().Be("Some message.");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Given_CodeAndMessage_When_CreatingConflict_Then_ReturnsConflictError()
    {
        var error = Error.Conflict("Some.Code", "Some message.");

        error.Code.Should().Be("Some.Code");
        error.Message.Should().Be("Some message.");
        error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Given_CodeAndMessage_When_CreatingForbidden_Then_ReturnsForbiddenError()
    {
        var error = Error.Forbidden("Listing.NotOwner", "Not allowed");

        error.Code.Should().Be("Listing.NotOwner");
        error.Message.Should().Be("Not allowed");
        error.Type.Should().Be(ErrorType.Forbidden);
    }
}
