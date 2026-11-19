using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Listings;

public sealed record Money
{
    public const decimal MaxAmount = 100_000m;

    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Result<Money> Create(decimal amount)
    {
        if (amount <= 0)
            return Result<Money>.Failure(Error.Validation("Money.Invalid", "Price must be greater than 0."));

        if (amount > MaxAmount)
            return Result<Money>.Failure(Error.Validation("Money.TooHigh", $"Price must be at most {MaxAmount} lei."));

        return new Money(amount);
    }
}
