using Microsoft.ML.Data;

namespace CampusMarket.Api.Infrastructure.ML;

public sealed class CategoryPrediction
{
    // The category name ("Textbook", "Notes", ...): the saved model ends with MapKeyToValue.
    [ColumnName("PredictedLabel")]
    public string PredictedLabel { get; set; } = string.Empty;

    // One probability per category; the highest one belongs to PredictedLabel.
    [ColumnName("Score")]
    public float[] Score { get; set; } = [];
}
