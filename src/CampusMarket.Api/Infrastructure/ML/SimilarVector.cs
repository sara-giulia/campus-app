using Microsoft.ML.Data;

namespace CampusMarket.Api.Infrastructure.ML;

public sealed class SimilarVector
{
    // The text vector of the listing, already of length 1 (SimilarTrainer normalises it).
    [ColumnName("Vector")]
    public float[] Vector { get; set; } = [];
}
