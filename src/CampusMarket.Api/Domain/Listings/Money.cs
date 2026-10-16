using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Listings;

public sealed record Money
{
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Result<Money> Create(decimal amount)
    {
        if (amount <= 0)
            return Result<Money>.Failure(
                new Error("Money.Invalid", "Price must be greater than 0."));

        return Result<Money>.Success(new Money(amount));
    }
}