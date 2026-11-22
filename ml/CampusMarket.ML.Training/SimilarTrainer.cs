using System.Globalization;
using System.Text;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CampusMarket.ML.Training;

// Similar listings: every listing becomes a text vector (FeaturizeText on its title, and possibly
// its description), normalised so that the dot product of two vectors is their cosine similarity.
// The saved model is that text-to-vector pipeline, learnt on the train part (vocabulary and
// weights); the API turns the target listing and the active listings into vectors and keeps the
// closest ones. Trained on non-spam rows only: spam is not something to recommend.
//
// Two variants are compared on validation with precision@5 (the share of the 5 closest other
// listings that have the same category, leaving out the listings of the same TemplateId), and
// the winner is measured once on test, next to what a random pick would score.
public sealed class SimilarTrainer : IModelTrainer
{
    public const string VectorColumn = "Vector";

    public enum SimilarVariant
    {
        TitleOnly,
        TitleAndDescription,
    }

    public string Name => "Similar listings (text vectors, cosine similarity)";

    public string ModelFileName => "similar-listings.zip";

    public TrainingResult Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData)
    {
        var trainNonSpam = WithoutSpam(mlContext, trainData);

        var results = new List<(SimilarVariant Variant, ITransformer Model, double Validation)>();
        foreach (var variant in Enum.GetValues<SimilarVariant>())
        {
            var model = BuildPipeline(mlContext, variant).Fit(trainNonSpam);
            results.Add((variant, model, PrecisionAt5(mlContext, model, validationData)));
        }

        // Ties keep the first (simpler) variant.
        var winner = results.OrderByDescending(r => r.Validation).First();
        var testItems = Items(mlContext, winner.Model, testData);
        var testPrecision = SimilarityEvaluation.PrecisionAtK(testItems);
        var randomPrecision = SimilarityEvaluation.RandomPrecision(testItems);

        var sb = new StringBuilder();
        sb.AppendLine("Precision@5 on validation (same category among the 5 closest listings of other item types):");
        sb.AppendLine("| Variant | Precision@5 |");
        sb.AppendLine("|---|---|");
        foreach (var r in results)
        {
            var mark = r.Variant == winner.Variant ? " ← chosen" : string.Empty;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {r.Variant}{mark} | {r.Validation:0.000} |"));
        }
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"On test ({testItems.Count} listings): precision@5 {testPrecision:0.000}, a random pick {randomPrecision:0.000}"));

        var metrics = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["precisionAt5"] = testPrecision,
            ["randomPrecisionAt5"] = randomPrecision,
        };

        return new TrainingResult(winner.Model, sb.ToString().TrimEnd(), winner.Variant.ToString(), metrics);
    }

    public static IEstimator<ITransformer> BuildPipeline(MLContext mlContext, SimilarVariant variant)
    {
        var title = mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(ListingRow.Title));

        IEstimator<ITransformer> features = variant == SimilarVariant.TitleOnly
            ? title.Append(mlContext.Transforms.Concatenate(VectorColumn, "TitleFeatures"))
            : title
                .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(ListingRow.Description)))
                .Append(mlContext.Transforms.Concatenate(VectorColumn, "TitleFeatures", "DescriptionFeatures"));

        // Unit length, so the dot product of two vectors is their cosine similarity.
        return features.Append(mlContext.Transforms.NormalizeLpNorm(VectorColumn));
    }

    // The non-spam rows of a part as evaluation items: vector, category and TemplateId.
    public static List<SimilarityEvaluation.Item> Items(MLContext mlContext, ITransformer model, IDataView data) =>
        mlContext.Data.CreateEnumerable<VectorRow>(model.Transform(WithoutSpam(mlContext, data)), reuseRowObject: false)
            .Select(r => new SimilarityEvaluation.Item(SimilarityEvaluation.SparseVector.FromDense(r.Vector), r.Category, r.TemplateId))
            .ToList();

    private static double PrecisionAt5(MLContext mlContext, ITransformer model, IDataView data) =>
        SimilarityEvaluation.PrecisionAtK(Items(mlContext, model, data));

    private static IDataView WithoutSpam(MLContext mlContext, IDataView data) =>
        mlContext.Data.FilterByCustomPredicate<ListingRow>(data, row => row.IsSpam);

    private sealed class VectorRow
    {
        public string Category { get; set; } = string.Empty;

        public string TemplateId { get; set; } = string.Empty;

        [ColumnName(VectorColumn)]
        public float[] Vector { get; set; } = [];
    }
}
