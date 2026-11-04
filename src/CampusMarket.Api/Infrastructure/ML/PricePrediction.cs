using Microsoft.ML.Data;

namespace CampusMarket.Api.Infrastructure.ML;

public sealed class PricePrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}
