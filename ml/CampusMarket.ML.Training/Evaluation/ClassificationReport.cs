using System.Globalization;
using System.Text;

namespace CampusMarket.ML.Training.Evaluation;

// Evaluation framework for the two classifiers (category and spam). Plain computations on
// (actual, predicted, probability) rows, so it works on any data and has no ML.NET dependency:
//  - multiclass (category): confusion matrix, precision / recall / F1 per class, macro-F1;
//  - binary (spam): confusion counts, precision, recall, F1 and PR-AUC (average precision);
//  - calibration for both: when the model says 90%, is it right about 90% of the time?
//    The probabilities are cut into equal bins and the mean predicted probability of each bin is
//    compared with how often the event really happened there; ECE is the weighted mean gap.
public static class ClassificationReport
{
    public const int DefaultCalibrationBins = 10;

    // ---- Multiclass ---------------------------------------------------------------------------

    public sealed record MulticlassPrediction(string Actual, string Predicted, double Confidence);

    public sealed record ClassMetrics(string Label, int Support, int PredictedCount, double Precision, double Recall, double F1);

    public sealed record MulticlassReport(
        IReadOnlyList<string> Labels,
        IReadOnlyList<IReadOnlyList<int>> Confusion,
        IReadOnlyList<ClassMetrics> PerClass,
        double Accuracy,
        double MacroF1,
        CalibrationReport Calibration);

    // Labels: the classes to report, in this order (rows and columns of the confusion matrix).
    // A label that is neither in the data nor predicted is left out of the macro-F1.
    public static MulticlassReport Multiclass(
        IReadOnlyList<MulticlassPrediction> predictions,
        IReadOnlyList<string> labels,
        int calibrationBins = DefaultCalibrationBins)
    {
        var index = labels.Select((label, i) => (label, i)).ToDictionary(x => x.label, x => x.i, StringComparer.Ordinal);
        var confusion = new int[labels.Count, labels.Count];
        foreach (var p in predictions)
        {
            if (!index.TryGetValue(p.Actual, out var row))
                throw new ArgumentException($"Actual label '{p.Actual}' is not in the label list.", nameof(predictions));
            if (!index.TryGetValue(p.Predicted, out var column))
                throw new ArgumentException($"Predicted label '{p.Predicted}' is not in the label list.", nameof(predictions));
            confusion[row, column]++;
        }

        var perClass = new List<ClassMetrics>(labels.Count);
        for (var c = 0; c < labels.Count; c++)
        {
            var truePositives = confusion[c, c];
            var support = Enumerable.Range(0, labels.Count).Sum(k => confusion[c, k]);
            var predicted = Enumerable.Range(0, labels.Count).Sum(k => confusion[k, c]);
            var precision = Ratio(truePositives, predicted);
            var recall = Ratio(truePositives, support);
            perClass.Add(new ClassMetrics(labels[c], support, predicted, precision, recall, F1(precision, recall)));
        }

        var counted = perClass.Where(m => m.Support > 0 || m.PredictedCount > 0).ToList();
        var macroF1 = counted.Count == 0 ? 0 : counted.Average(m => m.F1);
        var correct = Enumerable.Range(0, labels.Count).Sum(c => confusion[c, c]);
        var accuracy = Ratio(correct, predictions.Count);

        var calibration = Calibration(
            predictions.Select(p => (p.Confidence, string.Equals(p.Actual, p.Predicted, StringComparison.Ordinal))).ToList(),
            calibrationBins);

        // Rows of the matrix as read-only lists: a public record should not expose a multidimensional array.
        var rows = Enumerable.Range(0, labels.Count)
            .Select(r => (IReadOnlyList<int>)Enumerable.Range(0, labels.Count).Select(c => confusion[r, c]).ToList())
            .ToList();

        return new MulticlassReport(labels, rows, perClass, accuracy, macroF1, calibration);
    }

    // ---- Binary -------------------------------------------------------------------------------

    public sealed record BinaryPrediction(bool Actual, double Probability);

    public sealed record BinaryReport(
        double Threshold,
        int TruePositives,
        int FalsePositives,
        int TrueNegatives,
        int FalseNegatives,
        double Accuracy,
        double Precision,
        double Recall,
        double F1,
        double PrAuc,
        CalibrationReport Calibration);

    // A row counts as positive when its probability is at least the threshold.
    public static BinaryReport Binary(
        IReadOnlyList<BinaryPrediction> predictions,
        double threshold = 0.5,
        int calibrationBins = DefaultCalibrationBins)
    {
        int tp = 0, fp = 0, tn = 0, fn = 0;
        foreach (var p in predictions)
        {
            var positive = p.Probability >= threshold;
            if (positive && p.Actual) tp++;
            else if (positive) fp++;
            else if (p.Actual) fn++;
            else tn++;
        }

        var precision = Ratio(tp, tp + fp);
        var recall = Ratio(tp, tp + fn);
        var calibration = Calibration(predictions.Select(p => (p.Probability, p.Actual)).ToList(), calibrationBins);

        return new BinaryReport(
            threshold, tp, fp, tn, fn,
            Ratio(tp + tn, predictions.Count),
            precision, recall, F1(precision, recall),
            AveragePrecision(predictions),
            calibration);
    }

    // PR-AUC as average precision: walk the rows from the highest probability down and add the
    // precision at every point where recall grows, weighted by how much it grew. Rows with the
    // same probability are taken together, so their order does not matter. No positives → 0.
    public static double AveragePrecision(IReadOnlyList<BinaryPrediction> predictions)
    {
        var positives = predictions.Count(p => p.Actual);
        if (positives == 0)
            return 0;

        double sum = 0;
        int tp = 0, seen = 0;
        foreach (var group in predictions.GroupBy(p => p.Probability).OrderByDescending(g => g.Key))
        {
            var groupPositives = group.Count(p => p.Actual);
            tp += groupPositives;
            seen += group.Count();
            if (groupPositives > 0)
                sum += (double)groupPositives / positives * tp / seen;
        }

        return sum;
    }

    // ---- Calibration --------------------------------------------------------------------------

    public sealed record CalibrationBin(double Lower, double Upper, int Count, double MeanPredicted, double ObservedRate);

    public sealed record CalibrationReport(IReadOnlyList<CalibrationBin> Bins, double ExpectedCalibrationError);

    // Items: (predicted probability that the event happens, whether it happened).
    // Equal-width bins over [0, 1]; a probability of exactly 1 goes in the last bin.
    // Empty bins are kept (Count 0) so the table always has the same rows.
    public static CalibrationReport Calibration(IReadOnlyList<(double Probability, bool Happened)> items, int bins = DefaultCalibrationBins)
    {
        if (bins < 1)
            throw new ArgumentOutOfRangeException(nameof(bins), bins, "At least one bin is needed.");

        var groups = items
            .GroupBy(item => Math.Clamp((int)(Math.Clamp(item.Probability, 0, 1) * bins), 0, bins - 1))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<CalibrationBin>(bins);
        double weightedGap = 0;
        for (var b = 0; b < bins; b++)
        {
            var lower = (double)b / bins;
            var upper = (double)(b + 1) / bins;
            if (!groups.TryGetValue(b, out var inBin))
            {
                result.Add(new CalibrationBin(lower, upper, 0, 0, 0));
                continue;
            }

            var meanPredicted = inBin.Average(i => i.Probability);
            var observed = inBin.Count(i => i.Happened) / (double)inBin.Count;
            weightedGap += inBin.Count * Math.Abs(meanPredicted - observed);
            result.Add(new CalibrationBin(lower, upper, inBin.Count, meanPredicted, observed));
        }

        return new CalibrationReport(result, items.Count == 0 ? 0 : weightedGap / items.Count);
    }

    // ---- Markdown -----------------------------------------------------------------------------

    public static string ToMarkdown(MulticlassReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Confusion matrix (rows = true, columns = predicted):");
        sb.Append("| true \\ predicted |");
        foreach (var label in report.Labels) sb.Append(' ').Append(label).Append(" |");
        sb.AppendLine();
        sb.Append("|---|");
        foreach (var _ in report.Labels) sb.Append("---|");
        sb.AppendLine();
        for (var r = 0; r < report.Labels.Count; r++)
        {
            sb.Append("| ").Append(report.Labels[r]).Append(" |");
            for (var c = 0; c < report.Labels.Count; c++)
                sb.Append(' ').Append(report.Confusion[r][c].ToString(CultureInfo.InvariantCulture)).Append(" |");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("| Class | Support | Precision | Recall | F1 |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var m in report.PerClass)
            sb.AppendLine(Inv($"| {m.Label} | {m.Support} | {m.Precision:0.000} | {m.Recall:0.000} | {m.F1:0.000} |"));
        sb.AppendLine(Inv($"| **Macro-F1** | | | | **{report.MacroF1:0.000}** |"));
        sb.AppendLine(Inv($"Accuracy: {report.Accuracy:0.000}"));

        sb.AppendLine();
        sb.AppendLine("Calibration (confidence = probability of the predicted class; observed = how often it was right):");
        AppendCalibration(sb, report.Calibration, "Mean confidence", "Observed accuracy");
        return sb.ToString().TrimEnd();
    }

    public static string ToMarkdown(BinaryReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Inv($"Threshold {report.Threshold:0.00}: TP {report.TruePositives}, FP {report.FalsePositives}, TN {report.TrueNegatives}, FN {report.FalseNegatives}"));
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|---|---|");
        sb.AppendLine(Inv($"| Accuracy | {report.Accuracy:0.000} |"));
        sb.AppendLine(Inv($"| Precision | {report.Precision:0.000} |"));
        sb.AppendLine(Inv($"| Recall | {report.Recall:0.000} |"));
        sb.AppendLine(Inv($"| F1 | {report.F1:0.000} |"));
        sb.AppendLine(Inv($"| PR-AUC | {report.PrAuc:0.000} |"));

        sb.AppendLine();
        sb.AppendLine("Calibration (predicted = probability of spam; observed = share that really is spam):");
        AppendCalibration(sb, report.Calibration, "Mean predicted", "Observed rate");
        return sb.ToString().TrimEnd();
    }

    private static void AppendCalibration(StringBuilder sb, CalibrationReport calibration, string predictedHeader, string observedHeader)
    {
        sb.AppendLine($"| Bin | Rows | {predictedHeader} | {observedHeader} |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var bin in calibration.Bins.Where(b => b.Count > 0))
            sb.AppendLine(Inv($"| {bin.Lower:0.0}–{bin.Upper:0.0} | {bin.Count} | {bin.MeanPredicted:0.000} | {bin.ObservedRate:0.000} |"));
        sb.AppendLine(Inv($"ECE (expected calibration error): {calibration.ExpectedCalibrationError:0.000}"));
    }

    private static double Ratio(int part, int whole) => whole == 0 ? 0 : (double)part / whole;

    private static double F1(double precision, double recall) =>
        precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
