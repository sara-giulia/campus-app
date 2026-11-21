using System.Globalization;
using Microsoft.ML;

namespace CampusMarket.ML.Training.Evaluation;

// 5-fold cross-validation grouped by TemplateId. Each fold holds out whole item
// types, so the metrics reflect how the model would score on unseen templates.
// This is the same guarantee DatasetSplit offers for the final train /
// validation / test split, applied at fold level instead.
//
// The three overloads cover the three ML.NET tasks used in the project:
// regression (price), multiclass (category) and binary (spam). They all run
// ML.NET CrossValidate with samplingKeyColumnName = TemplateId and the project
// seed, and return mean ± std for the headline metrics.
public static class CrossValidation
{
    public const int DefaultNumberOfFolds = 5;

    public sealed record MetricStats(string Name, double Mean, double StdDev)
    {
        public string Format(string valueFormat = "0.000", string unit = "")
        {
            var mean = Mean.ToString(valueFormat, CultureInfo.InvariantCulture);
            var std = StdDev.ToString(valueFormat, CultureInfo.InvariantCulture);
            var suffix = string.IsNullOrEmpty(unit) ? string.Empty : " " + unit;
            return $"{Name}: {mean} ± {std}{suffix}";
        }
    }

    public static IReadOnlyList<MetricStats> Regression<T>(
        MLContext mlContext,
        IDataView data,
        IEstimator<T> pipeline,
        string labelColumnName = "Label",
        int numberOfFolds = DefaultNumberOfFolds)
        where T : class, ITransformer
    {
        var results = mlContext.Regression.CrossValidate(
            data,
            pipeline,
            numberOfFolds: numberOfFolds,
            labelColumnName: labelColumnName,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: DatasetSplit.Seed);

        return new[]
        {
            Stat("R²", results.Select(r => r.Metrics.RSquared)),
            Stat("MAE", results.Select(r => r.Metrics.MeanAbsoluteError)),
            Stat("RMSE", results.Select(r => r.Metrics.RootMeanSquaredError)),
        };
    }

    public static IReadOnlyList<MetricStats> Multiclass<T>(
        MLContext mlContext,
        IDataView data,
        IEstimator<T> pipeline,
        string labelColumnName = "Label",
        int numberOfFolds = DefaultNumberOfFolds)
        where T : class, ITransformer
    {
        var results = mlContext.MulticlassClassification.CrossValidate(
            data,
            pipeline,
            numberOfFolds: numberOfFolds,
            labelColumnName: labelColumnName,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: DatasetSplit.Seed);

        return new[]
        {
            Stat("Micro accuracy", results.Select(r => r.Metrics.MicroAccuracy)),
            Stat("Macro accuracy", results.Select(r => r.Metrics.MacroAccuracy)),
            Stat("Log loss", results.Select(r => r.Metrics.LogLoss)),
        };
    }

    public static IReadOnlyList<MetricStats> Binary<T>(
        MLContext mlContext,
        IDataView data,
        IEstimator<T> pipeline,
        string labelColumnName,
        int numberOfFolds = DefaultNumberOfFolds)
        where T : class, ITransformer
    {
        var results = mlContext.BinaryClassification.CrossValidateNonCalibrated(
            data,
            pipeline,
            numberOfFolds: numberOfFolds,
            labelColumnName: labelColumnName,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: DatasetSplit.Seed);

        return new[]
        {
            Stat("Accuracy", results.Select(r => r.Metrics.Accuracy)),
            Stat("AUC", results.Select(r => r.Metrics.AreaUnderRocCurve)),
            Stat("F1 score", results.Select(r => r.Metrics.F1Score)),
        };
    }

    private static MetricStats Stat(string name, IEnumerable<double> values)
    {
        var arr = values.ToArray();
        var mean = arr.Average();
        var variance = arr.Sum(v => (v - mean) * (v - mean)) / arr.Length;
        return new MetricStats(name, mean, Math.Sqrt(variance));
    }
}
