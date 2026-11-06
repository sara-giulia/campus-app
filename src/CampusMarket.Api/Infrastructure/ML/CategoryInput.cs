namespace CampusMarket.Api.Infrastructure.ML;

// Column names must match those the trainer sees (ListingRow in CampusMarket.ML.Training).
// The category model reads only the title and the description.
public sealed class CategoryInput
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
