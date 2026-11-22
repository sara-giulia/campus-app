using Microsoft.ML.Data;

namespace CampusMarket.ML.Training;

public sealed class ListingRow
{
    [LoadColumn(0)]
    public string Title { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string Description { get; set; } = string.Empty;

    [LoadColumn(2)]
    public string Category { get; set; } = string.Empty;

    [LoadColumn(3)]
    public string Condition { get; set; } = string.Empty;

    [LoadColumn(4)]
    public float Price { get; set; }

    [LoadColumn(5)]
    public bool IsSpam { get; set; }

    [LoadColumn(6)]
    public string TemplateId { get; set; } = string.Empty;
}
