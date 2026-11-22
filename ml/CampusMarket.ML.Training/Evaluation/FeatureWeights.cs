using System.Globalization;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Calibrators;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace CampusMarket.ML.Training.Evaluation;

// Explainability: which words weigh the most in the two linear models.
//  - category (SDCA maximum entropy): one weight per class for every feature; a high positive
//    weight for a class means the word pushes the prediction towards that class;
//  - spam (SDCA logistic regression): one weight per feature; positive pushes towards spam,
//    negative towards a normal listing; the price bins have their own weight.
// The features come from FeaturizeText: words and pairs of words ("Word"), plus groups of three
// letters ("Char"). Every text vector is normalised, so the weights of different words can be
// compared. Only the word features are listed: they are the ones a person can read.
public static class FeatureWeights
{
    public const int DefaultCount = 10;

    // Feature: the word, or the two words of a pair ("la 0738"). Source: Title or Description.
    public sealed record WeightedFeature(string Feature, string Source, bool IsWord, double Weight);

    // Slot names look like "TitleFeatures.Word.notite", "DescriptionFeatures.Word.la|0738"
    // or "TitleFeatures.Char.a|b|c"; anything else (for example "PriceFeatures") is kept as it is.
    public static WeightedFeature Describe(string slotName, double weight)
    {
        var parts = slotName.Split('.', 3);
        if (parts.Length < 3)
            return new WeightedFeature(slotName, slotName, false, weight);

        var source = parts[0].EndsWith("Features", StringComparison.Ordinal) ? parts[0][..^"Features".Length] : parts[0];
        var isWord = parts[1] == "Word";
        var feature = isWord ? parts[2].Replace('|', ' ') : parts[2];
        return new WeightedFeature(feature, source, isWord, weight);
    }

    // The `count` word features with the highest weights (towardPositive) or the lowest ones.
    // Equal weights keep the order of the slots, so the result does not change between runs.
    public static IReadOnlyList<WeightedFeature> Top(
        IReadOnlyList<string> slotNames,
        IReadOnlyList<double> weights,
        int count,
        bool towardPositive = true)
    {
        if (slotNames.Count != weights.Count)
            throw new ArgumentException("There must be one weight for every slot name.", nameof(weights));

        var words = slotNames
            .Select((name, i) => Describe(name, weights[i]))
            .Where(f => f.IsWord && (towardPositive ? f.Weight > 0 : f.Weight < 0));

        var ordered = towardPositive
            ? words.OrderByDescending(f => f.Weight)
            : words.OrderBy(f => f.Weight);

        return ordered.Take(count).ToList();
    }

    // ---- the two models ----

    public static IReadOnlyDictionary<string, IReadOnlyList<WeightedFeature>> ForCategory(
        ITransformer model, DataViewSchema inputSchema, int count = DefaultCount)
    {
        var predictor = Steps(model).OfType<MulticlassPredictionTransformer<MaximumEntropyModelParameters>>().Single();
        var outputSchema = model.GetOutputSchema(inputSchema);

        // The class of every row of weights is the matching slot name of the Score column.
        var classes = SlotNames(outputSchema["Score"]);
        var names = SlotNames(outputSchema["Features"]);

        VBuffer<float>[] weights = [];
        predictor.Model.GetWeights(ref weights, out var classCount);

        var result = new SortedDictionary<string, IReadOnlyList<WeightedFeature>>(StringComparer.Ordinal);
        for (var c = 0; c < classCount; c++)
            result[classes[c]] = Top(names, Dense(weights[c]), count);
        return result;
    }

    public sealed record SpamWeights(
        IReadOnlyList<WeightedFeature> TowardSpam,
        IReadOnlyList<WeightedFeature> TowardNormal,
        double PriceWeight);

    public static SpamWeights ForSpam(ITransformer model, DataViewSchema inputSchema, int count = DefaultCount)
    {
        var predictor = Steps(model)
            .OfType<BinaryPredictionTransformer<CalibratedModelParametersBase<LinearBinaryModelParameters, PlattCalibrator>>>()
            .Single();

        var names = SlotNames(model.GetOutputSchema(inputSchema)["Features"]);
        var weights = predictor.Model.SubModel.Weights.Select(w => (double)w).ToList();

        var priceIndex = names.ToList().IndexOf("PriceFeatures");
        var priceWeight = priceIndex >= 0 ? weights[priceIndex] : 0;

        return new SpamWeights(Top(names, weights, count), Top(names, weights, count, towardPositive: false), priceWeight);
    }

    // A freshly fitted pipeline is a TransformerChain<LastStepType>, a loaded one a
    // TransformerChain<ITransformer>; both are a sequence of transformers.
    private static IEnumerable<ITransformer> Steps(ITransformer model) =>
        model as IEnumerable<ITransformer>
        ?? throw new ArgumentException("The model must be a chain of transformers.", nameof(model));

    private static List<string> SlotNames(DataViewSchema.Column column)
    {
        VBuffer<ReadOnlyMemory<char>> names = default;
        column.GetSlotNames(ref names);
        return names.DenseValues().Select(n => n.ToString()).ToList();
    }

    private static List<double> Dense(VBuffer<float> vector) => vector.DenseValues().Select(w => (double)w).ToList();

    // ---- markdown ----

    public static string ToMarkdown(IReadOnlyDictionary<string, IReadOnlyList<WeightedFeature>> byClass)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Words that push the most towards each category (weight; T = title, D = description):");
        sb.AppendLine("| Category | Top words |");
        sb.AppendLine("|---|---|");
        foreach (var (category, features) in byClass)
            sb.AppendLine($"| {category} | {string.Join(", ", features.Select(Format))} |");
        return sb.ToString().TrimEnd();
    }

    public static string ToMarkdown(SpamWeights spam)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Words with the biggest weights in the spam model (T = title, D = description):");
        sb.AppendLine("| Towards spam | Weight | Towards a normal listing | Weight |");
        sb.AppendLine("|---|---|---|---|");
        var rows = Math.Max(spam.TowardSpam.Count, spam.TowardNormal.Count);
        for (var i = 0; i < rows; i++)
        {
            var toSpam = i < spam.TowardSpam.Count ? spam.TowardSpam[i] : null;
            var toNormal = i < spam.TowardNormal.Count ? spam.TowardNormal[i] : null;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {Label(toSpam)} | {toSpam?.Weight:0.00} | {Label(toNormal)} | {toNormal?.Weight:0.00} |"));
        }
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Price bins (one feature): weight {spam.PriceWeight:0.00}"));
        return sb.ToString().TrimEnd();
    }

    private static string Label(WeightedFeature? f) => f is null ? string.Empty : $"{f.Feature} ({f.Source[0]})";

    private static string Format(WeightedFeature f) =>
        string.Create(CultureInfo.InvariantCulture, $"{f.Feature} ({f.Source[0]}, {f.Weight:0.00})");
}
