namespace CampusMarket.Api.Infrastructure.ML;

// Column names must match those the trainer sees (ListingRow in CampusMarket.ML.Training).
// The similar-listings model reads the title and the description.
public sealed class SimilarInput
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
