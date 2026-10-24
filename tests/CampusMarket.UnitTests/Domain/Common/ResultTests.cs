using CampusMarket.Api.Domain.Common;

namespace CampusMarket.UnitTests.Domain.Common;

public class ResultTests
{
    [Fact]
    public void Given_Value_When_CreatingSuccess_Then_IsSuccessAndExposesValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Given_Error_When_CreatingFailure_Then_IsFailureAndExposesError()
    {
        var error = Error.Validation("Some.Code", "Some message.");

        var result = Result<int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Given_FailedResult_When_ReadingValue_Then_ThrowsInvalidOperation()
    {
        var result = Result<int>.Failure(Error.Validation("Some.Code", "Some message."));

        Action act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_SuccessfulResult_When_ReadingError_Then_ThrowsInvalidOperation()
    {
        var result = Result<int>.Success(42);

        Action act = () => _ = result.Error;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Given_Value_When_ConvertingImplicitly_Then_ReturnsSuccessWithThatValue()
    {
        Result<int> result = 7;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7);
    }
}
