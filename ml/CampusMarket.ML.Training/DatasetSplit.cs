using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Shared 70/15/15 train/validation/test split used both by the training
// program and by the model quality tests. Keeping it here means the split
// and the seed live in one place and cannot drift.
public static class DatasetSplit
{
    public const int Seed = 2026;

    // 70% train, 15% validation, 15% test. First split holds out 30%,
    // the second cuts that 30% in half.
    public const double FirstTestFraction = 0.3;

    public const double SecondTestFraction = 0.5;

    public static (IDataView Train, IDataView Validation, IDataView Test) Split(MLContext mlContext, IDataView data)
    {
        var firstSplit = mlContext.Data.TrainTestSplit(data, testFraction: FirstTestFraction, seed: Seed);
        var secondSplit = mlContext.Data.TrainTestSplit(firstSplit.TestSet, testFraction: SecondTestFraction, seed: Seed);
        return (firstSplit.TrainSet, secondSplit.TrainSet, secondSplit.TestSet);
    }
}
