using System.Globalization;
using System.Text;
using static CampusMarket.ML.Training.Evaluation.ClassificationReport;

namespace CampusMarket.ML.Training.Evaluation;

// Chooses the two decision thresholds on the VALIDATION part (never on test), so they are
// settings picked from data instead of numbers written in the code:
//  - category: the minimum confidence under which the API shows no suggestion. The smallest
//    threshold at which the suggestions still shown are right at least TargetAccuracy of the
//    time; smaller means more suggestions shown, so the smallest good one is kept.
//  - spam: the probability from which a listing is reported as spam. The threshold with the best
//    F1, the balance between spam let through (recall) and good listings flagged (precision);
//    when several thresholds share the best F1, the middle one, so it is not on the edge.
// The API reads them from configuration (Ml:Category:MinConfidence, Ml:Spam:Threshold).
public static class ThresholdSelection
{
    // When a category suggestion is shown, it should be right at least 9 times out of 10.
    public const double TargetAccuracy = 0.90;

    public static readonly IReadOnlyList<double> CategoryCandidates = Candidates(0.30, 0.95, 0.05);

    public static readonly IReadOnlyList<double> SpamCandidates = Candidates(0.05, 0.95, 0.05);

    public static IReadOnlyList<double> Candidates(double from, double to, double step)
    {
        if (step <= 0 || to < from)
            throw new ArgumentOutOfRangeException(nameof(step), "The step must be positive and from must not exceed to.");

        var count = (int)Math.Round((to - from) / step) + 1;
        return Enumerable.Range(0, count).Select(i => Math.Round(from + i * step, 2)).ToList();
    }

    // ---- category: minimum confidence ----

    // Coverage: share of rows whose confidence reaches the threshold (a suggestion is shown).
    // Accuracy: how many of those shown suggestions are right (0 when nothing is shown).
    public sealed record ConfidenceRow(double Threshold, int Shown, double Coverage, double Accuracy);

    public static IReadOnlyList<ConfidenceRow> ConfidenceTable(
        IReadOnlyList<MulticlassPrediction> predictions,
        IReadOnlyList<double> candidates)
    {
        return candidates.Select(t =>
        {
            var shown = predictions.Where(p => p.Confidence >= t).ToList();
            var right = shown.Count(p => string.Equals(p.Actual, p.Predicted, StringComparison.Ordinal));
            return new ConfidenceRow(
                t,
                shown.Count,
                predictions.Count == 0 ? 0 : (double)shown.Count / predictions.Count,
                shown.Count == 0 ? 0 : (double)right / shown.Count);
        }).ToList();
    }

    // The smallest threshold whose shown suggestions reach the target accuracy; when none does,
    // the highest candidate (show as few doubtful suggestions as possible).
    public static double ChooseMinConfidence(IReadOnlyList<ConfidenceRow> table, double targetAccuracy = TargetAccuracy)
    {
        if (table.Count == 0)
            throw new ArgumentException("At least one candidate threshold is needed.", nameof(table));

        var good = table.Where(r => r.Shown > 0 && r.Accuracy >= targetAccuracy).OrderBy(r => r.Threshold).FirstOrDefault();
        return good?.Threshold ?? table.Max(r => r.Threshold);
    }

    // ---- spam: decision threshold ----

    public sealed record SpamRow(double Threshold, double Precision, double Recall, double F1, int FalsePositives, int FalseNegatives);

    public static IReadOnlyList<SpamRow> SpamTable(IReadOnlyList<BinaryPrediction> predictions, IReadOnlyList<double> candidates)
    {
        return candidates.Select(t =>
        {
            var report = Binary(predictions, t);
            return new SpamRow(t, report.Precision, report.Recall, report.F1, report.FalsePositives, report.FalseNegatives);
        }).ToList();
    }

    public static double ChooseSpamThreshold(IReadOnlyList<SpamRow> table)
    {
        if (table.Count == 0)
            throw new ArgumentException("At least one candidate threshold is needed.", nameof(table));

        var bestF1 = table.Max(r => r.F1);
        var best = table.Where(r => Math.Abs(r.F1 - bestF1) < 1e-12).OrderBy(r => r.Threshold).ToList();
        return best[(best.Count - 1) / 2].Threshold;
    }

    // ---- markdown ----

    public static string ToMarkdown(IReadOnlyList<ConfidenceRow> table, double chosen)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Min confidence | Shown | Coverage | Accuracy of shown |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var r in table)
            sb.AppendLine(Row(r.Threshold, chosen, $"{r.Shown} | {r.Coverage:0.000} | {r.Accuracy:0.000}"));
        return sb.ToString().TrimEnd();
    }

    public static string ToMarkdown(IReadOnlyList<SpamRow> table, double chosen)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Threshold | Precision | Recall | F1 | Good flagged (FP) | Spam missed (FN) |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var r in table)
            sb.AppendLine(Row(r.Threshold, chosen, $"{r.Precision:0.000} | {r.Recall:0.000} | {r.F1:0.000} | {r.FalsePositives} | {r.FalseNegatives}"));
        return sb.ToString().TrimEnd();
    }

    private static string Row(double threshold, double chosen, FormattableString rest)
    {
        var mark = Math.Abs(threshold - chosen) < 1e-9 ? " ← chosen" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"| {threshold:0.00}{mark} | {rest.ToString(CultureInfo.InvariantCulture)} |");
    }
}
