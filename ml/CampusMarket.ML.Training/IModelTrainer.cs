using Microsoft.ML;

namespace CampusMarket.ML.Training;

public interface IModelTrainer
{
    string Name { get; }

    string ModelFileName { get; }

    // train fits the model, validation is for choosing settings, test is used once for the final metrics.
    TrainingResult Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData);
}

// What the training program needs from a trainer: the fitted model, the human-
// readable metrics table for the console, and the structured test metrics plus
// the chosen algorithm/target that go into the model's .json sidecar.
public sealed record TrainingResult(
    ITransformer Model,
    string MetricsText,
    string Algorithm,
    IReadOnlyDictionary<string, double> TestMetrics);
