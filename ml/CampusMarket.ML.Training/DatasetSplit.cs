using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Shared 70/15/15 train/validation/test split used both by the training
// program and by the model quality tests. Keeping it here means the split
// and the seed live in one place and cannot drift.
//
// The split is group-aware: both TrainTestSplit calls pass samplingKeyColumnName
// = TemplateId, so every row of an item type (template) ends up in exactly one
// of train, validation or test. The quality tests therefore evaluate on item
// types the model has never seen.
//
// ML.NET hashes the samplingKey column internally. If we pass the same seed to
// both cuts, the second one would hash each TemplateId to the same value as the
// first and shove every leftover row into one of the two new buckets, so the
// second cut uses Seed + 1 for an independent hash. The seed is still kept in
// this one place so training and tests use the same partitioning.
public static class DatasetSplit
{
    public const int Seed = 2026;

    // 70% train, 15% validation, 15% test. First split holds out 30%,
    // the second cuts that 30% in half.
    public const double FirstTestFraction = 0.3;

    public const double SecondTestFraction = 0.5;

    public static (IDataView Train, IDataView Validation, IDataView Test) Split(MLContext mlContext, IDataView data)
    {
        var firstSplit = mlContext.Data.TrainTestSplit(
            data,
            testFraction: FirstTestFraction,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: Seed);

        var secondSplit = mlContext.Data.TrainTestSplit(
            firstSplit.TestSet,
            testFraction: SecondTestFraction,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: Seed + 1);

        return (firstSplit.TrainSet, secondSplit.TrainSet, secondSplit.TestSet);
    }
}
