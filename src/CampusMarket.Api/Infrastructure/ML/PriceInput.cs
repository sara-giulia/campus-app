namespace CampusMarket.Api.Infrastructure.ML;

// Column names must match those the trainer sees (ListingRow in CampusMarket.ML.Training).
public sealed class PriceInput
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;
}
