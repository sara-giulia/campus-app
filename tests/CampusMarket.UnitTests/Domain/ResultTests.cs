using CampusMarket.Api.Domain.Common;
using Xunit;

namespace CampusMarket.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_ShouldExposeValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ImplicitConversion_ShouldCreateSuccess()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Success_ReadingError_ShouldThrow()
    {
        var result = Result<int>.Success(42);

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_ShouldExposeError()
    {
        var error = Error.Validation("Test.Code", "Test message");

        var result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Failure_ReadingValue_ShouldThrow()
    {
        var result = Result<int>.Failure(Error.Validation("Test.Code", "Test message"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ErrorFactories_ShouldSetTheRightType()
    {
        var validation = Error.Validation("A", "a");
        var notFound = Error.NotFound("B", "b");
        var conflict = Error.Conflict("C", "c");

        Assert.Equal(ErrorType.Validation, validation.Type);
        Assert.Equal(ErrorType.NotFound, notFound.Type);
        Assert.Equal(ErrorType.Conflict, conflict.Type);
        Assert.Equal("B", notFound.Code);
        Assert.Equal("c", conflict.Message);
    }
}
