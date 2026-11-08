using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Price regression trainer. Trains FastTree on non-spam rows only; spam prices are
// absurd on purpose and would bias the model. Features: Category + Condition one-hot
// encoded, Title and Description featurized as text. Label: Price (or log1p(Price)
// when LogLabel is true). Metrics (R², MAE, RMSE) are always reported in the original
// price space so the two variants can be compared directly. When LogLabel is true the
// API undoes the transform with exp(score) - 1; the saved model itself has no custom
// mapping, so it loads in the API without a registered contract.
public sealed class PriceTrainer(bool logLabel = false) : IModelTrainer
{
    public bool LogLabel { get; } = logLabel;

    public string Name => LogLabel ? "Price regression (FastTree, log1p)" : "Price regression (FastTree)";

    public string ModelFileName => "price-regression.zip";

    public (ITransformer Model, string Metrics) Train(MLContext mlContext, IDataView trainData, IDataView testData)
    {
        var trainNonSpam = PrepareData(mlContext, trainData);
        var testNonSpam = PrepareData(mlContext, testData);

        var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("CategoryEncoded", nameof(ListingRow.Category))
            .Append(mlContext.Transforms.Categorical.OneHotEncoding("ConditionEncoded", nameof(ListingRow.Condition)))
            .Append(mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(ListingRow.Title)))
            .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(ListingRow.Description)))
            .Append(mlContext.Transforms.Concatenate("Features",
                "CategoryEncoded", "ConditionEncoded", "TitleFeatures", "DescriptionFeatures"))
            .Append(mlContext.Regression.Trainers.FastTree(labelColumnName: "Label", featureColumnName: "Features"));

        // ITransformer, the type that IModelTrainer returns for every model
        ITransformer model = pipeline.Fit(trainNonSpam);

        var metrics = Evaluate(mlContext, model, testNonSpam);
        var text =
            $"R²:   {metrics.R2:0.000}" + Environment.NewLine +
            $"MAE:  {metrics.Mae:0.00} lei" + Environment.NewLine +
            $"RMSE: {metrics.Rmse:0.00} lei";

        return (model, text);
    }

    private IDataView PrepareData(MLContext mlContext, IDataView data)
    {
        var rows = mlContext.Data.CreateEnumerable<ListingRow>(data, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .Select(r => new TrainingRow
            {
                Title = r.Title,
                Description = r.Description,
                Category = r.Category,
                Condition = r.Condition,
                Price = r.Price,
                Label = LogLabel ? MathF.Log(r.Price + 1f) : r.Price
            })
            .ToList();
        return mlContext.Data.LoadFromEnumerable(rows);
    }

    private PriceMetrics Evaluate(MLContext mlContext, ITransformer model, IDataView test)
    {
        var scored = model.Transform(test);
        var rows = mlContext.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false).ToList();

        var actual = rows.Select(r => (double)r.Price).ToArray();
        var predicted = rows.Select(r => LogLabel ? Math.Max(0, Math.Exp(r.Score) - 1) : r.Score).ToArray();

        var mean = actual.Average();
        double ssTotal = 0;
        double ssRes = 0;
        double sumAbs = 0;
        double sumSq = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            var err = actual[i] - predicted[i];
            ssTotal += (actual[i] - mean) * (actual[i] - mean);
            ssRes += err * err;
            sumAbs += Math.Abs(err);
            sumSq += err * err;
        }

        var r2 = ssTotal > 0 ? 1 - ssRes / ssTotal : 0;
        var mae = sumAbs / actual.Length;
        var rmse = Math.Sqrt(sumSq / actual.Length);

        return new PriceMetrics(r2, mae, rmse);
    }

    private sealed record PriceMetrics(double R2, double Mae, double Rmse);

    private sealed class TrainingRow
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Condition { get; set; } = string.Empty;

        public float Price { get; set; }

        public float Label { get; set; }
    }

    private sealed class ScoredRow
    {
        public float Price { get; set; }

        public float Score { get; set; }
    }
}
