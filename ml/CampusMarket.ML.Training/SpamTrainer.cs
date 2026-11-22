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

        // Decision threshold chosen on validation, then measured once on test, on its own MLContext
        // (see CategoryTrainer: the shared one would hand different random seeds to later trainers).
        var selection = new MLContext(seed: DatasetSplit.Seed);
        var validationTable = ThresholdSelection.SpamTable(
            Predictions(selection, model.Transform(validationData)), ThresholdSelection.SpamCandidates);
        var threshold = ThresholdSelection.ChooseSpamThreshold(validationTable);
        var atThreshold = ClassificationReport.Binary(Predictions(selection, scored), threshold);

        var text =
            $"Accuracy:  {metrics.Accuracy:F3}{Environment.NewLine}" +
            $"AUC:       {metrics.AreaUnderRocCurve:F3}{Environment.NewLine}" +
            $"F1 score:  {metrics.F1Score:F3}{Environment.NewLine}" +
            $"Precision: {metrics.PositivePrecision:F3}{Environment.NewLine}" +
            $"Recall:    {metrics.PositiveRecall:F3}{Environment.NewLine}" +
            Environment.NewLine +
            ClassificationReport.ToMarkdown(report) + Environment.NewLine +
            Environment.NewLine +
            "Spam threshold, chosen on validation (best F1; the middle one when several tie):" + Environment.NewLine +
            ThresholdSelection.ToMarkdown(validationTable, threshold) + Environment.NewLine +
            FormattableString.Invariant($"On test at {threshold:0.00}: precision {atThreshold.Precision:0.000}, recall {atThreshold.Recall:0.000}, F1 {atThreshold.F1:0.000} (FP {atThreshold.FalsePositives}, FN {atThreshold.FalseNegatives}).") + Environment.NewLine +
            FormattableString.Invariant($"API setting: Ml:Spam:Threshold = {threshold:0.00}");

        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["accuracy"] = metrics.Accuracy,
            ["auc"] = metrics.AreaUnderRocCurve,
            ["ece"] = report.Calibration.ExpectedCalibrationError,
            ["f1"] = metrics.F1Score,
            ["f1AtThreshold"] = atThreshold.F1,
            ["prAuc"] = report.PrAuc,
            ["precision"] = metrics.PositivePrecision,
            ["recall"] = metrics.PositiveRecall,
        };

        var settings = new Dictionary<string, double>(StringComparer.Ordinal) { ["threshold"] = threshold };

        return new TrainingResult(model, text, "SDCA logistic regression", structured, settings);
    }

    // Precision, recall, F1, PR-AUC and calibration of P(spam) on the scored test rows,
    // with the model's own decision threshold (0.5).
    public static ClassificationReport.BinaryReport Report(MLContext mlContext, IDataView scored) =>
        ClassificationReport.Binary(Predictions(mlContext, scored));

    // (is it spam, probability of spam) for every scored row.
    public static List<ClassificationReport.BinaryPrediction> Predictions(MLContext mlContext, IDataView scored) =>
        mlContext.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false)
            .Select(r => new ClassificationReport.BinaryPrediction(r.IsSpam, r.Probability))
            .ToList();

    private sealed class ScoredRow
    {
        public bool IsSpam { get; set; }

        public float Probability { get; set; }
    }
}
