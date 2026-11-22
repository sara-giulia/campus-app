using System.Globalization;
using System.Text;
using Microsoft.ML;

namespace CampusMarket.ML.Training.Evaluation;

// Error analysis for the model cards (docs/ml/category.md, docs/ml/spam.md): the mistakes worth
// reading, listed from the scored rows so they can be produced again after every retrain.
//  - category: the wrong predictions made with the highest confidence. Those are the ones the API
//    really shows (confidence at or above the minimum), so they matter more than the unsure ones.
//  - spam: the normal listings flagged as spam (false positives, highest probability first) and
//    the spam let through (false negatives, lowest probability first), at the chosen threshold.
public static class ErrorAnalysis
{
    public const int DefaultCount = 10;

    public sealed record CategoryMistake(string Title, string Actual, string Predicted, double Confidence);

    public sealed record SpamMistake(string Title, string Description, float Price, bool IsSpam, double Probability);

    public sealed record SpamMistakes(IReadOnlyList<SpamMistake> FlaggedNormal, IReadOnlyList<SpamMistake> MissedSpam);

    // Wrong predictions, most confident first; equal confidences keep the order of the rows.
    public static IReadOnlyList<CategoryMistake> MostConfidentMistakes(IEnumerable<CategoryMistake> rows, int count = DefaultCount) =>
        rows.Where(r => !string.Equals(r.Actual, r.Predicted, StringComparison.Ordinal))
            .OrderByDescending(r => r.Confidence)
            .Take(count)
            .ToList();

    // A row is predicted as spam when its probability reaches the threshold.
    public static SpamMistakes SpamErrors(IReadOnlyList<SpamMistake> rows, double threshold, int count = DefaultCount) =>
        new(
            rows.Where(r => !r.IsSpam && r.Probability >= threshold).OrderByDescending(r => r.Probability).Take(count).ToList(),
            rows.Where(r => r.IsSpam && r.Probability < threshold).OrderBy(r => r.Probability).Take(count).ToList());

    // ---- scoring with the models ----

    public static IReadOnlyList<CategoryMistake> CategoryMistakes(MLContext mlContext, ITransformer model, IEnumerable<ListingRow> rows, int count = DefaultCount)
    {
        var nonSpam = mlContext.Data.LoadFromEnumerable(rows.Where(r => !r.IsSpam).ToList());
        var scored = mlContext.Data.CreateEnumerable<CategoryScored>(model.Transform(nonSpam), reuseRowObject: false)
            .Select(r => new CategoryMistake(r.Title, r.Category, r.PredictedLabel, r.Score.Max()));
        return MostConfidentMistakes(scored, count);
    }

    public static SpamMistakes SpamMistakesOf(MLContext mlContext, ITransformer model, IEnumerable<ListingRow> rows, double threshold, int count = DefaultCount)
    {
        var data = mlContext.Data.LoadFromEnumerable(rows.ToList());
        var scored = mlContext.Data.CreateEnumerable<SpamScored>(model.Transform(data), reuseRowObject: false)
            .Select(r => new SpamMistake(r.Title, r.Description, r.Price, r.IsSpam, r.Probability))
            .ToList();
        return SpamErrors(scored, threshold, count);
    }

    // ---- markdown ----

    public static string ToMarkdown(IReadOnlyList<CategoryMistake> mistakes)
    {
        if (mistakes.Count == 0)
            return "No wrong predictions.";

        var sb = new StringBuilder();
        sb.AppendLine("| Title | True | Predicted | Confidence |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var m in mistakes)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {Clean(m.Title)} | {m.Actual} | {m.Predicted} | {m.Confidence:0.00} |"));
        return sb.ToString().TrimEnd();
    }

    public static string ToMarkdown(SpamMistakes mistakes, double threshold)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Normal listings flagged as spam (probability ≥ {threshold:0.00}): {mistakes.FlaggedNormal.Count}"));
        AppendSpamTable(sb, mistakes.FlaggedNormal);
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Spam let through (probability < {threshold:0.00}): {mistakes.MissedSpam.Count}"));
        AppendSpamTable(sb, mistakes.MissedSpam);
        return sb.ToString().TrimEnd();
    }

    private static void AppendSpamTable(StringBuilder sb, IReadOnlyList<SpamMistake> rows)
    {
        if (rows.Count == 0)
            return;

        sb.AppendLine("| Title | Description | Price | Probability |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var r in rows)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {Clean(r.Title)} | {Clean(r.Description, 70)} | {r.Price:0} | {r.Probability:0.00} |"));
    }

    private static string Clean(string? text, int max = 80)
    {
        var flat = (text ?? string.Empty).Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        return flat.Length <= max ? flat : flat[..(max - 1)] + "…";
    }

    private sealed class CategoryScored
    {
        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string PredictedLabel { get; set; } = string.Empty;

        public float[] Score { get; set; } = [];
    }

    private sealed class SpamScored
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public float Price { get; set; }

        public bool IsSpam { get; set; }

        public float Probability { get; set; }
    }
}
