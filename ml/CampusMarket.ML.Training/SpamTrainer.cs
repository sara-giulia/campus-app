using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Binary classification: is a listing spam? Trained on all rows (spam and not spam).
// Spam has its own words (links, phone numbers, "!!!") and absurd prices (1 leu or tens of thousands),
// so the features are the two texts plus the price split into bins.
public sealed class SpamTrainer : IModelTrainer
{
    public string Name => "Spam detection (SDCA logistic regression)";

    public string ModelFileName => "spam-detection.zip";

    public TrainingResult Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData)
    {
        // The label is IsSpam itself, so the saved model does not need an IsSpam column at prediction time.
        var pipeline = mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(ListingRow.Title))
            .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(ListingRow.Description)))
            .Append(mlContext.Transforms.NormalizeBinning("PriceFeatures", nameof(ListingRow.Price), maximumBinCount: 20))
            .Append(mlContext.Transforms.Concatenate("Features", "TitleFeatures", "DescriptionFeatures", "PriceFeatures"))
            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                new Microsoft.ML.Trainers.SdcaLogisticRegressionBinaryTrainer.Options
                {
                    LabelColumnName = nameof(ListingRow.IsSpam),
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1,
                }));

        var model = pipeline.Fit(trainData);
        var scored = model.Transform(testData);
        var metrics = mlContext.BinaryClassification.Evaluate(scored, labelColumnName: nameof(ListingRow.IsSpam));
        var report = Report(mlContext, scored);

        var text =
            $"Accuracy:  {metrics.Accuracy:F3}{Environment.NewLine}" +
            $"AUC:       {metrics.AreaUnderRocCurve:F3}{Environment.NewLine}" +
            $"F1 score:  {metrics.F1Score:F3}{Environment.NewLine}" +
            $"Precision: {metrics.PositivePrecision:F3}{Environment.NewLine}" +
            $"Recall:    {metrics.PositiveRecall:F3}{Environment.NewLine}" +
            Environment.NewLine +
            ClassificationReport.ToMarkdown(report);

        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["accuracy"] = metrics.Accuracy,
            ["auc"] = metrics.AreaUnderRocCurve,
            ["ece"] = report.Calibration.ExpectedCalibrationError,
            ["f1"] = metrics.F1Score,
            ["prAuc"] = report.PrAuc,
            ["precision"] = metrics.PositivePrecision,
            ["recall"] = metrics.PositiveRecall,
        };

        return new TrainingResult(model, text, "SDCA logistic regression", structured);
    }

    // Precision, recall, F1, PR-AUC and calibration of P(spam) on the scored test rows,
    // with the model's own decision threshold (0.5).
    public static ClassificationReport.BinaryReport Report(MLContext mlContext, IDataView scored) =>
        ClassificationReport.Binary(
            mlContext.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false)
                .Select(r => new ClassificationReport.BinaryPrediction(r.IsSpam, r.Probability))
                .ToList());

    private sealed class ScoredRow
    {
        public bool IsSpam { get; set; }

        public float Probability { get; set; }
    }
}
