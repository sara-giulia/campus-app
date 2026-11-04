using Microsoft.ML;

namespace CampusMarket.ML.Training;

public interface IModelTrainer
{
    string Name { get; }

    string ModelFileName { get; }

    (ITransformer Model, string Metrics) Train(MLContext mlContext, IDataView trainData, IDataView testData);
}
