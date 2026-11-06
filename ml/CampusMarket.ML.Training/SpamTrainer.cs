using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Binary classification: is a listing spam? Trained on all rows (spam and not spam).
// Spam has its own words (links, phone numbers, "!!!") and absurd prices (1 leu or tens of thousands),
// so the features are the two texts plus the price split into bins.
public sealed class SpamTrainer : IModelTrainer
{
    public string Name => "Spam detection (SDCA logistic regression)";

    public string ModelFileName => "spam-detection.zip";

    public (ITransformer Model, string Metrics) Train(MLContext mlContext, IDataView trainData, IDataView testData)
    {
        // The label is IsSpam itself, so the saved model does not need an IsSpam column at prediction time.
        var pipeline = mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(ListingRow.Title))
            .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(ListingRow.Description)))
            .Append(mlContext.Transforms.NormalizeBinning("PriceFeatures", nameof(ListingRow.Price), maximumBinCount: 20))
            .Append(mlContext.Transforms.Concatenate("Features", "TitleFeatures", "DescriptionFeatures", "PriceFeatures"))
            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: nameof(ListingRow.IsSpam), featureColumnName: "Features"));

        var model = pipeline.Fit(trainData);
        var metrics = mlContext.BinaryClassification.Evaluate(model.Transform(testData), labelColumnName: nameof(ListingRow.IsSpam));

        var text =
            $"Accuracy:  {metrics.Accuracy:F3}{Environment.NewLine}" +
            $"AUC:       {metrics.AreaUnderRocCurve:F3}{Environment.NewLine}" +
            $"F1 score:  {metrics.F1Score:F3}{Environment.NewLine}" +
            $"Precision: {metrics.PositivePrecision:F3}{Environment.NewLine}" +
            $"Recall:    {metrics.PositiveRecall:F3}";

        return (model, text);
    }
}
