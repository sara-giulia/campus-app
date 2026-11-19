using Microsoft.ML;

namespace CampusMarket.ML.Training;

public interface IModelTrainer
{
    string Name { get; }

    string ModelFileName { get; }

    // train fits the model, validation is for choosing settings, test is used once for the final metrics.
    (ITransformer Model, string Metrics) Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData);
}
