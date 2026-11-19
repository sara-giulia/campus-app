using System.Text.RegularExpressions;
using Microsoft.ML;

namespace CampusMarket.ML.Training.Evaluation;

// Trivial baselines for the three ML tasks, evaluated on the same test part
// as the trained models (DatasetSplit, group-aware by TemplateId). A model
// that fails to beat these baselines is not learning anything they miss, so
// the training program prints a model-vs-baselines table at the end.
//
// The same metric records are also used by the test-set evaluation of the
// trained models, so the table shows both sides in the same shape.
public static class Baselines
{
    public sealed record PriceMetrics(double R2, double Mae, double Rmse);

    public sealed record CategoryMetrics(double MicroAccuracy, double MacroAccuracy);

    public sealed record SpamMetrics(double Accuracy, double Precision, double Recall, double F1);

    // --- Baselines ----------------------------------------------------------

    // Price (non-spam rows): predict the mean price of the same Category x
    // Condition from the train part. Fallbacks: Category mean, global mean.
    public static PriceMetrics Price(MLContext mlContext, IDataView trainData, IDataView testData)
    {
        var train = NonSpam(mlContext, trainData);
        var test = NonSpam(mlContext, testData);

        var catCondMean = train
            .GroupBy(r => (r.Category, r.Condition))
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Price));

        var catMean = train
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Price));

        var globalMean = train.Average(r => (double)r.Price);

        double Predict(ListingRow r)
        {
            if (catCondMean.TryGetValue((r.Category, r.Condition), out var v)) return v;
            if (catMean.TryGetValue(r.Category, out var cv)) return cv;
            return globalMean;
        }

        var actual = test.Select(r => (double)r.Price).ToArray();
        var predicted = test.Select(Predict).ToArray();
        return Regression(actual, predicted);
    }

    // Category (non-spam rows), baseline (a): always the most frequent class
    // of the train part.
    public static CategoryMetrics CategoryMostFrequent(MLContext mlContext, IDataView trainData, IDataView testData)
    {
        var train = NonSpam(mlContext, trainData);
        var test = NonSpam(mlContext, testData);

        var mostFrequent = train
            .GroupBy(r => r.Category)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return Multiclass(test.Select(r => r.Category), test.Select(_ => mostFrequent));
    }

    // Category (non-spam rows), baseline (b): short keyword rule in Romanian.
    public static CategoryMetrics CategoryKeywords(MLContext mlContext, IDataView testData)
    {
        var test = NonSpam(mlContext, testData);
        return Multiclass(test.Select(r => r.Category), test.Select(KeywordCategory));
    }

    private static string KeywordCategory(ListingRow r)
    {
        var text = ((r.Title ?? string.Empty) + " " + (r.Description ?? string.Empty)).ToLowerInvariant();
        if (ContainsAny(text, "carte", "manual", "curs")) return "Textbook";
        if (ContainsAny(text, "notite", "rezumat")) return "Notes";
        if (ContainsAny(text, "laptop", "telefon", "casti")) return "Electronics";
        if (ContainsAny(text, "microscop", "halat", "pipeta")) return "LabEquipment";
        return "Other";
    }

    // Spam (all rows): hand-written rule — description or title contains a
    // phone number, a link, whatsapp/telegram, 3+ exclamation marks, or
    // the price is <= 5 lei.
    public static SpamMetrics Spam(MLContext mlContext, IDataView testData)
    {
        var rows = mlContext.Data.CreateEnumerable<ListingRow>(testData, reuseRowObject: false).ToList();
        var predicted = rows.Select(IsSpamByRule).ToArray();
        var actual = rows.Select(r => r.IsSpam).ToArray();
        return Binary(actual, predicted);
    }

    private static readonly Regex PhoneRegex =
        new(@"(?:\d[\s\-\.]*){9,}", RegexOptions.Compiled);

    private static readonly Regex LinkRegex =
        new(@"(?:https?://|www\.|\blink\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExclRegex =
        new(@"!{3,}", RegexOptions.Compiled);

    private static bool IsSpamByRule(ListingRow r)
    {
        var text = (r.Title ?? string.Empty) + " " + (r.Description ?? string.Empty);
        var lower = text.ToLowerInvariant();
        if (PhoneRegex.IsMatch(text)) return true;
        if (LinkRegex.IsMatch(text)) return true;
        if (lower.Contains("whatsapp") || lower.Contains("telegram")) return true;
        if (ExclRegex.IsMatch(text)) return true;
        if (r.Price <= 5f) return true;
        return false;
    }

    // --- Model evaluation on the same test set -----------------------------

    // Price model: filter non-spam, score, compute R² / MAE / RMSE from the
    // Score column against the Price column. The FastTree trainer does not
    // need Label at Transform time.
    public static PriceMetrics EvaluatePriceModel(MLContext mlContext, ITransformer model, IDataView testData)
    {
        var rows = NonSpam(mlContext, testData);
        var view = mlContext.Data.LoadFromEnumerable(rows);
        var scored = model.Transform(view);
        var scoredRows = mlContext.Data.CreateEnumerable<PriceScoredRow>(scored, reuseRowObject: false).ToList();

        var actual = scoredRows.Select(r => (double)r.Price).ToArray();
        var predicted = scoredRows.Select(r => (double)r.Score).ToArray();
        return Regression(actual, predicted);
    }

    // Category model: filter non-spam, score, read PredictedLabel (string,
    // since the pipeline ends with MapKeyToValue).
    public static CategoryMetrics EvaluateCategoryModel(MLContext mlContext, ITransformer model, IDataView testData)
    {
        var rows = NonSpam(mlContext, testData);
        var view = mlContext.Data.LoadFromEnumerable(rows);
        var scored = model.Transform(view);
        var scoredRows = mlContext.Data.CreateEnumerable<CategoryScoredRow>(scored, reuseRowObject: false).ToList();

        return Multiclass(
            scoredRows.Select(r => r.Category),
            scoredRows.Select(r => r.PredictedLabel));
    }

    // Spam model: evaluate on all rows; PredictedLabel is bool.
    public static SpamMetrics EvaluateSpamModel(MLContext mlContext, ITransformer model, IDataView testData)
    {
        var scored = model.Transform(testData);
        var scoredRows = mlContext.Data.CreateEnumerable<SpamScoredRow>(scored, reuseRowObject: false).ToList();

        var actual = scoredRows.Select(r => r.IsSpam).ToArray();
        var predicted = scoredRows.Select(r => r.PredictedLabel).ToArray();
        return Binary(actual, predicted);
    }

    // --- Shared helpers ----------------------------------------------------

    private static List<ListingRow> NonSpam(MLContext mlContext, IDataView data) =>
        mlContext.Data.CreateEnumerable<ListingRow>(data, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .ToList();

    private static PriceMetrics Regression(double[] actual, double[] predicted)
    {
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

    private static CategoryMetrics Multiclass(IEnumerable<string> actual, IEnumerable<string> predicted)
    {
        var a = actual.ToList();
        var p = predicted.ToList();
        var correct = 0;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] == p[i]) correct++;
        }
        var micro = a.Count > 0 ? (double)correct / a.Count : 0;

        var classes = a.Distinct().ToList();
        double macroSum = 0;
        var seen = 0;
        foreach (var c in classes)
        {
            var total = 0;
            var good = 0;
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != c) continue;
                total++;
                if (p[i] == c) good++;
            }
            if (total > 0)
            {
                macroSum += (double)good / total;
                seen++;
            }
        }
        var macro = seen > 0 ? macroSum / seen : 0;
        return new CategoryMetrics(micro, macro);
    }

    private static SpamMetrics Binary(bool[] actual, bool[] predicted)
    {
        var tp = 0;
        var fp = 0;
        var tn = 0;
        var fn = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            if (predicted[i] && actual[i]) tp++;
            else if (predicted[i] && !actual[i]) fp++;
            else if (!predicted[i] && actual[i]) fn++;
            else tn++;
        }
        var total = actual.Length;
        var accuracy = total > 0 ? (double)(tp + tn) / total : 0;
        var precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
        var recall = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        var f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0;
        return new SpamMetrics(accuracy, precision, recall, f1);
    }

    private static bool ContainsAny(string text, params string[] words)
    {
        foreach (var w in words)
        {
            if (text.Contains(w)) return true;
        }
        return false;
    }

    private sealed class PriceScoredRow
    {
        public float Price { get; set; }

        public float Score { get; set; }
    }

    private sealed class CategoryScoredRow
    {
        public string Category { get; set; } = string.Empty;

        public string PredictedLabel { get; set; } = string.Empty;
    }

    private sealed class SpamScoredRow
    {
        public bool IsSpam { get; set; }

        public bool PredictedLabel { get; set; }
    }
}
