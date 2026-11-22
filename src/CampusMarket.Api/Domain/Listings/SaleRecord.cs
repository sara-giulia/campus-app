using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Listings;

// One row per confirmed sale: what the price suggester asked for vs. what the
// seller actually sold the listing for. The price-accuracy endpoint reads
// these to measure how well the model tracks the market over time.
public sealed class SaleRecord
{
    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Category Category { get; private set; }
    public Condition Condition { get; private set; }
    public decimal SuggestedPrice { get; private set; }
    public decimal SoldPrice { get; private set; }
    public DateTimeOffset SoldAt { get; private set; }

    private SaleRecord() { }

    public static Result<SaleRecord> Create(
        Guid listingId,
        Category category,
        Condition condition,
        decimal suggestedPrice,
        decimal soldPrice,
        DateTimeOffset soldAt)
    {
        if (listingId == Guid.Empty)
            return Result<SaleRecord>.Failure(
                Error.Validation("SaleRecord.ListingEmpty", "Listing id must not be empty."));

        if (suggestedPrice <= 0)
            return Result<SaleRecord>.Failure(
                Error.Validation("SaleRecord.SuggestedPriceInvalid", "Suggested price must be greater than 0."));

        if (soldPrice <= 0)
            return Result<SaleRecord>.Failure(
                Error.Validation("SaleRecord.SoldPriceInvalid", "Sold price must be greater than 0."));

        return new SaleRecord
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            Category = category,
            Condition = condition,
            SuggestedPrice = suggestedPrice,
            SoldPrice = soldPrice,
            SoldAt = soldAt,
        };
    }
}
