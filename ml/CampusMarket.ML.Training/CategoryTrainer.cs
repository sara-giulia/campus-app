using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Category classification trainer (B4). Multiclass SDCA (maximum entropy) on the non-spam rows only:
// spam rows have a category, but their text has nothing to do with it and would only add noise.
// Features: Title and Description through FeaturizeText. Label: Category.
//
// The Category -> key mapping is fitted on its own and is NOT part of the saved model, so the model
// only needs Title and Description at prediction time. The saved model ends with MapKeyToValue, so
// PredictedLabel is the category name ("Textbook", ...) and Score holds one probability per category.
public sealed class CategoryTrainer : IModelTrainer
{
    public string Name => "Category classification (SDCA maximum entropy)";

    public string ModelFileName => "category-classification.zip";

    public TrainingResult Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData)
    {
        var trainNonSpam = WithoutSpam(mlContext, trainData);
        var testNonSpam = WithoutSpam(mlContext, testData);

        var labelMap = mlContext.Transforms.Conversion
            .MapValueToKey("Label", nameof(ListingRow.Category))
            .Fit(trainNonSpam);

        var pipeline = mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(ListingRow.Title))
            .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(ListingRow.Description)))
            .Append(mlContext.Transforms.Concatenate("Features", "TitleFeatures", "DescriptionFeatures"))
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                new Microsoft.ML.Trainers.SdcaMaximumEntropyMulticlassTrainer.Options
                {
                    LabelColumnName = "Label",
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1,
                }))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(labelMap.Transform(trainNonSpam));

        var scored = model.Transform(labelMap.Transform(testNonSpam));
        var metrics = mlContext.MulticlassClassification.Evaluate(scored, labelColumnName: "Label");

        var report = Report(mlContext, scored, trainNonSpam);

        // Minimum confidence chosen on validation, then measured once on test. The validation work uses
        // its own MLContext: every component created on the shared one draws from its random generator,
        // which would change the models trained after this one.
        var selection = new MLContext(seed: DatasetSplit.Seed);
        var validationTable = ThresholdSelection.ConfidenceTable(
            Predictions(selection, model.Transform(WithoutSpam(selection, validationData))), ThresholdSelection.CategoryCandidates);
        var minConfidence = ThresholdSelection.ChooseMinConfidence(validationTable);
        var onTest = ThresholdSelection.ConfidenceTable(Predictions(selection, scored), [minConfidence]).Single();

        // Explainability: the words with the biggest weight for each category.
        var topWords = FeatureWeights.ForCategory(model, labelMap.Transform(trainNonSpam).Schema);

        var text =
            $"Micro accuracy: {metrics.MicroAccuracy:0.000}" + Environment.NewLine +
            $"Macro accuracy: {metrics.MacroAccuracy:0.000}" + Environment.NewLine +
            $"Log loss:       {metrics.LogLoss:0.000}" + Environment.NewLine +
            Environment.NewLine +
            ClassificationReport.ToMarkdown(report) + Environment.NewLine +
            Environment.NewLine +
            FormattableString.Invariant($"Minimum confidence, chosen on validation (smallest threshold whose shown suggestions are right at least {ThresholdSelection.TargetAccuracy:0%} of the time):") + Environment.NewLine +
            ThresholdSelection.ToMarkdown(validationTable, minConfidence) + Environment.NewLine +
            FormattableString.Invariant($"On test at {minConfidence:0.00}: {onTest.Coverage:0.0%} of the listings get a suggestion, right {onTest.Accuracy:0.0%} of the time.") + Environment.NewLine +
            FormattableString.Invariant($"API setting: Ml:Category:MinConfidence = {minConfidence:0.00}") + Environment.NewLine +
            Environment.NewLine +
            FeatureWeights.ToMarkdown(topWords);

        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["accuracyAtMinConfidence"] = onTest.Accuracy,
            ["coverageAtMinConfidence"] = onTest.Coverage,
            ["ece"] = report.Calibration.ExpectedCalibrationError,
            ["logLoss"] = metrics.LogLoss,
            ["macroAccuracy"] = metrics.MacroAccuracy,
            ["macroF1"] = report.MacroF1,
            ["microAccuracy"] = metrics.MicroAccuracy,
        };

        var settings = new Dictionary<string, double>(StringComparer.Ordinal) { ["minConfidence"] = minConfidence };

        return new TrainingResult(model, text, "SDCA maximum entropy", structured, settings);
    }

    private static IDataView WithoutSpam(MLContext mlContext, IDataView data) =>
        mlContext.Data.FilterByCustomPredicate<ListingRow>(data, row => row.IsSpam);

    // Confusion matrix, per-class metrics, macro-F1 and calibration on the scored test rows.
    // The confidence of a row is the probability of the class the model chose.
    public static ClassificationReport.MulticlassReport Report(MLContext mlContext, IDataView scored, IDataView trainNonSpam)
    {
        var labels = mlContext.Data.CreateEnumerable<ListingRow>(trainNonSpam, reuseRowObject: false)
            .Select(r => r.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        return ClassificationReport.Multiclass(Predictions(mlContext, scored), labels);
    }

    // (true category, predicted category, probability of the predicted category) for every scored row.
    public static List<ClassificationReport.MulticlassPrediction> Predictions(MLContext mlContext, IDataView scored) =>
        mlContext.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false)
            .Select(r => new ClassificationReport.MulticlassPrediction(r.Category, r.PredictedLabel, r.Score.Max()))
            .ToList();

    private sealed class ScoredRow
    {
        public string Category { get; set; } = string.Empty;

        public string PredictedLabel { get; set; } = string.Empty;

        public float[] Score { get; set; } = [];
    }
}
