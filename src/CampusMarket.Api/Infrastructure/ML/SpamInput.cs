namespace CampusMarket.Api.Infrastructure.ML;

// The columns the spam model reads. The names match ListingRow in the training project.
public sealed class SpamInput
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public float Price { get; set; }
}
