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

        var text =
            $"Micro accuracy: {metrics.MicroAccuracy:0.000}" + Environment.NewLine +
            $"Macro accuracy: {metrics.MacroAccuracy:0.000}" + Environment.NewLine +
            $"Log loss:       {metrics.LogLoss:0.000}";

        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["logLoss"] = metrics.LogLoss,
            ["macroAccuracy"] = metrics.MacroAccuracy,
            ["microAccuracy"] = metrics.MicroAccuracy,
        };

        return new TrainingResult(model, text, "SDCA maximum entropy", structured);
    }

    private static IDataView WithoutSpam(MLContext mlContext, IDataView data) =>
        mlContext.Data.FilterByCustomPredicate<ListingRow>(data, row => row.IsSpam);
}
