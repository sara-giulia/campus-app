using Microsoft.ML.Data;

namespace CampusMarket.Api.Infrastructure.ML;

public sealed class SpamPrediction
{
    [ColumnName("PredictedLabel")]
    public bool IsSpam { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }
}
