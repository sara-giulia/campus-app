using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Listings;

public class MoneyTests
{
    [Fact]
    public void Given_PositiveAmount_When_Creating_Then_ReturnsMoneyWithThatAmount()
    {
        var result = Money.Create(25m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(25m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Given_ZeroOrNegativeAmount_When_Creating_Then_ReturnsValidationError(decimal amount)
    {
        var result = Money.Create(amount);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Money.Invalid");
        result.Error.Message.Should().Be("Price must be greater than 0.");
    }

    [Fact]
    public void Given_AmountEqualToMaxAmount_When_Creating_Then_ReturnsMoneyWithThatAmount()
    {
        var result = Money.Create(Money.MaxAmount);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(Money.MaxAmount);
    }

    [Fact]
    public void Given_AmountAboveMaxAmount_When_Creating_Then_ReturnsValidationError()
    {
        var result = Money.Create(Money.MaxAmount + 0.01m);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Money.TooHigh");
        result.Error.Message.Should().Be($"Price must be at most {Money.MaxAmount} lei.");
    }
}
