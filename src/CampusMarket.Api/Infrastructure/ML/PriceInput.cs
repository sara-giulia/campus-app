namespace CampusMarket.Api.Infrastructure.ML;

// Column names must match those the trainer sees (ListingRow in CampusMarket.ML.Training).
// Brand, StorageGb, CpuTier and IsNewOrSealed are the explicit text features (PriceTextFeatures);
// a model trained on the Current feature set simply ignores them.
public sealed class PriceInput
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;

    public string Brand { get; set; } = PriceTextFeatures.NoBrand;

    public float StorageGb { get; set; }

    public float CpuTier { get; set; }

    public float IsNewOrSealed { get; set; }
}
