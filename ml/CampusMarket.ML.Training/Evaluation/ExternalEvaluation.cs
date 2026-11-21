using System.Globalization;
using System.Text;
using Microsoft.ML;

namespace CampusMarket.ML.Training.Evaluation;

// External evaluation: loads the three SAVED models (the same .zip files the
// API uses) from ml/models/ and evaluates them on ml/data/external.csv,
// alongside the trivial baselines from Baselines.cs. The same metrics are
// also recomputed on the grouped test part so the final table can show
// test vs external side by side. Nothing here changes the models.
//
// Called from Program.cs after training; no-op when external.csv is missing.
public static class ExternalEvaluation
{
    public static void Run(
        MLContext mlContext,
        string externalCsvPath,
        string modelsDir,
        IDataView trainData,
        IDataView testData)
    {
        if (!File.Exists(externalCsvPath))
        {
            return;
        }

        var externalData = mlContext.Data.LoadFromTextFile<ListingRow>(
            externalCsvPath,
            hasHeader: true,
            separatorChar: ',',
            allowQuoting: true);

        var externalRows = mlContext.Data.CreateEnumerable<ListingRow>(externalData, reuseRowObject: false).ToList();

        var priceModel = mlContext.Model.Load(Path.Combine(modelsDir, "price-regression.zip"), out _);
        var categoryModel = mlContext.Model.Load(Path.Combine(modelsDir, "category-classification.zip"), out _);
        var spamModel = mlContext.Model.Load(Path.Combine(modelsDir, "spam-detection.zip"), out _);

        // Price: non-spam
        var pricePredsExternal = PricePredictions(mlContext, priceModel, externalRows.Where(r => !r.IsSpam).ToList());
        var pricePredsTest = PricePredictions(mlContext, priceModel, NonSpam(mlContext, testData));
        var priceBaselineExternal = PriceBaselinePredictions(mlContext, trainData, externalRows.Where(r => !r.IsSpam).ToList());
        var priceBaselineTest = PriceBaselinePredictions(mlContext, trainData, NonSpam(mlContext, testData));

        var priceMetricsExt = RegressionMetrics(pricePredsExternal);
        var priceMetricsTest = RegressionMetrics(pricePredsTest);
        var priceBaselineMetricsExt = RegressionMetrics(priceBaselineExternal);
        var priceBaselineMetricsTest = RegressionMetrics(priceBaselineTest);

        // Category: non-spam
        var catPredsExternal = CategoryPredictions(mlContext, categoryModel, externalRows.Where(r => !r.IsSpam).ToList());
        var catPredsTest = CategoryPredictions(mlContext, categoryModel, NonSpam(mlContext, testData));
        var catMostFrequent = MostFrequentCategory(mlContext, trainData);
        var catBaselineExternal = externalRows.Where(r => !r.IsSpam).Select(r => (r, Predicted: catMostFrequent)).ToList();
        var catBaselineTest = NonSpam(mlContext, testData).Select(r => (r, Predicted: catMostFrequent)).ToList();

        var catMetricsExt = MulticlassMetrics(catPredsExternal);
        var catMetricsTest = MulticlassMetrics(catPredsTest);
        var catBaselineMetricsExt = MulticlassMetrics(catBaselineExternal);
        var catBaselineMetricsTest = MulticlassMetrics(catBaselineTest);
        var confusionExternal = ConfusionMatrix(catPredsExternal);

        // Spam: all rows
        var spamPredsExternal = SpamPredictions(mlContext, spamModel, externalRows);
        var spamPredsTest = SpamPredictions(mlContext, spamModel, mlContext.Data.CreateEnumerable<ListingRow>(testData, reuseRowObject: false).ToList());
        var spamBaselineExternal = externalRows.Select(r => (r, Predicted: IsSpamByRule(r))).ToList();
        var spamBaselineTest = mlContext.Data.CreateEnumerable<ListingRow>(testData, reuseRowObject: false)
            .Select(r => (r, Predicted: IsSpamByRule(r))).ToList();

        var spamMetricsExt = BinaryMetrics(spamPredsExternal);
        var spamMetricsTest = BinaryMetrics(spamPredsTest);
        var spamBaselineMetricsExt = BinaryMetrics(spamBaselineExternal);
        var spamBaselineMetricsTest = BinaryMetrics(spamBaselineTest);

        PrintCounts(externalRows);
        PrintTable(
            priceMetricsTest, priceMetricsExt, priceBaselineMetricsTest, priceBaselineMetricsExt,
            catMetricsTest, catMetricsExt, catBaselineMetricsTest, catBaselineMetricsExt,
            spamMetricsTest, spamMetricsExt, spamBaselineMetricsTest, spamBaselineMetricsExt);
        PrintConfusionMatrix(confusionExternal);
        PrintTopPriceErrors(pricePredsExternal);
        PrintMisclassifiedSpam(spamPredsExternal);
    }

    // --- Prediction helpers -----------------------------------------------

    private static List<(ListingRow Row, double Score)> PricePredictions(MLContext mlContext, ITransformer model, List<ListingRow> rows)
    {
        if (rows.Count == 0) return new List<(ListingRow, double)>();

        var view = mlContext.Data.LoadFromEnumerable(rows);
        var scored = model.Transform(view);
        var scoredRows = mlContext.Data.CreateEnumerable<PriceScored>(scored, reuseRowObject: false).ToList();

        var result = new List<(ListingRow, double)>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            result.Add((rows[i], Math.Max(0.0, scoredRows[i].Score)));
        }
        return result;
    }

    private static List<(ListingRow Row, string Predicted)> CategoryPredictions(MLContext mlContext, ITransformer model, List<ListingRow> rows)
    {
        if (rows.Count == 0) return new List<(ListingRow, string)>();

        var view = mlContext.Data.LoadFromEnumerable(rows);
        var scored = model.Transform(view);
        var scoredRows = mlContext.Data.CreateEnumerable<CategoryScored>(scored, reuseRowObject: false).ToList();

        var result = new List<(ListingRow, string)>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            result.Add((rows[i], scoredRows[i].PredictedLabel));
        }
        return result;
    }

    private static List<(ListingRow Row, bool Predicted)> SpamPredictions(MLContext mlContext, ITransformer model, List<ListingRow> rows)
    {
        if (rows.Count == 0) return new List<(ListingRow, bool)>();

        var view = mlContext.Data.LoadFromEnumerable(rows);
        var scored = model.Transform(view);
        var scoredRows = mlContext.Data.CreateEnumerable<SpamScored>(scored, reuseRowObject: false).ToList();

        var result = new List<(ListingRow, bool)>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            result.Add((rows[i], scoredRows[i].PredictedLabel));
        }
        return result;
    }

    // --- Baselines (matched to Baselines.cs) ------------------------------

    private static List<(ListingRow Row, double Score)> PriceBaselinePredictions(MLContext mlContext, IDataView trainData, List<ListingRow> rows)
    {
        var train = mlContext.Data.CreateEnumerable<ListingRow>(trainData, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .ToList();

        var catCondMean = train
            .GroupBy(r => (r.Category, r.Condition))
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Price));
        var catMean = train
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Price));
        var globalMean = train.Count > 0 ? train.Average(r => (double)r.Price) : 0;

        return rows.Select(r =>
        {
            double v;
            if (catCondMean.TryGetValue((r.Category, r.Condition), out var cc)) v = cc;
            else if (catMean.TryGetValue(r.Category, out var cv)) v = cv;
            else v = globalMean;
            return (r, v);
        }).ToList();
    }

    private static string MostFrequentCategory(MLContext mlContext, IDataView trainData) =>
        mlContext.Data.CreateEnumerable<ListingRow>(trainData, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .GroupBy(r => r.Category)
            .OrderByDescending(g => g.Count())
            .First().Key;

    // Spam rule — same patterns as Baselines.cs, so reuse its GeneratedRegex methods
    // instead of declaring duplicates here.
    private static bool IsSpamByRule(ListingRow r)
    {
        var text = (r.Title ?? string.Empty) + " " + (r.Description ?? string.Empty);
        var lower = text.ToLowerInvariant();
        if (Baselines.PhoneRegex().IsMatch(text)) return true;
        if (Baselines.LinkRegex().IsMatch(text)) return true;
        if (lower.Contains("whatsapp") || lower.Contains("telegram")) return true;
        if (Baselines.ExclRegex().IsMatch(text)) return true;
        if (r.Price <= 5f) return true;
        return false;
    }

    private static List<ListingRow> NonSpam(MLContext mlContext, IDataView data) =>
        mlContext.Data.CreateEnumerable<ListingRow>(data, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .ToList();

    // --- Metrics ----------------------------------------------------------

    public sealed record PriceMetrics(double R2, double Mae, double Mape);
    public sealed record MultiMetrics(double MicroAccuracy, double MacroAccuracy);
    public sealed record BinMetrics(double Accuracy, double Precision, double Recall, double F1);

    private static PriceMetrics RegressionMetrics(List<(ListingRow Row, double Score)> preds)
    {
        if (preds.Count == 0) return new PriceMetrics(0, 0, 0);
        var actual = preds.Select(p => (double)p.Row.Price).ToArray();
        var predicted = preds.Select(p => p.Score).ToArray();

        var mean = actual.Average();
        double ssTotal = 0, ssRes = 0, sumAbs = 0, mapeSum = 0;
        var mapeCount = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            var err = actual[i] - predicted[i];
            ssTotal += (actual[i] - mean) * (actual[i] - mean);
            ssRes += err * err;
            sumAbs += Math.Abs(err);
            if (actual[i] > 0)
            {
                mapeSum += Math.Abs(err) / actual[i];
                mapeCount++;
            }
        }
        var r2 = ssTotal > 0 ? 1 - ssRes / ssTotal : 0;
        var mae = sumAbs / actual.Length;
        var mape = mapeCount > 0 ? 100.0 * mapeSum / mapeCount : 0;
        return new PriceMetrics(r2, mae, mape);
    }

    private static MultiMetrics MulticlassMetrics(List<(ListingRow Row, string Predicted)> preds)
    {
        if (preds.Count == 0) return new MultiMetrics(0, 0);
        var correct = preds.Count(p => p.Row.Category == p.Predicted);
        var micro = (double)correct / preds.Count;

        var classes = preds.Select(p => p.Row.Category).Distinct().ToList();
        double macroSum = 0;
        var seen = 0;
        foreach (var c in classes)
        {
            var total = preds.Count(p => p.Row.Category == c);
            var good = preds.Count(p => p.Row.Category == c && p.Predicted == c);
            if (total > 0)
            {
                macroSum += (double)good / total;
                seen++;
            }
        }
        var macro = seen > 0 ? macroSum / seen : 0;
        return new MultiMetrics(micro, macro);
    }

    private static BinMetrics BinaryMetrics(List<(ListingRow Row, bool Predicted)> preds)
    {
        if (preds.Count == 0) return new BinMetrics(0, 0, 0, 0);
        var tp = 0; var fp = 0; var tn = 0; var fn = 0;
        foreach (var p in preds)
        {
            if (p.Predicted && p.Row.IsSpam) tp++;
            else if (p.Predicted && !p.Row.IsSpam) fp++;
            else if (!p.Predicted && p.Row.IsSpam) fn++;
            else tn++;
        }
        var accuracy = (double)(tp + tn) / preds.Count;
        var precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
        var recall = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        var f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0;
        return new BinMetrics(accuracy, precision, recall, f1);
    }

    private static readonly string[] Categories =
    {
        "Electronics", "LabEquipment", "Notes", "Other", "Textbook",
    };

    private static Dictionary<(string Actual, string Predicted), int> ConfusionMatrix(List<(ListingRow Row, string Predicted)> preds)
    {
        var m = new Dictionary<(string, string), int>();
        foreach (var p in preds)
        {
            var key = (p.Row.Category, p.Predicted);
            m[key] = m.TryGetValue(key, out var v) ? v + 1 : 1;
        }
        return m;
    }

    // --- Printing ---------------------------------------------------------

    private static void PrintCounts(List<ListingRow> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        Console.WriteLine();
        Console.WriteLine("=== External evaluation (ml/data/external.csv) ===");
        Console.WriteLine();
        Console.WriteLine(string.Create(inv,
            $"External rows: {rows.Count} total, {rows.Count(r => !r.IsSpam)} non-spam, {rows.Count(r => r.IsSpam)} spam."));

        Console.WriteLine();
        Console.WriteLine("Rows per category:");
        Console.WriteLine("| Category | Non-spam | Spam |");
        Console.WriteLine("|---|---|---|");
        foreach (var cat in Categories)
        {
            var ns = rows.Count(r => r.Category == cat && !r.IsSpam);
            var sp = rows.Count(r => r.Category == cat && r.IsSpam);
            Console.WriteLine(string.Create(inv, $"| {cat} | {ns} | {sp} |"));
        }
    }

    private static void PrintTable(
        PriceMetrics priceTest, PriceMetrics priceExt, PriceMetrics priceBaseTest, PriceMetrics priceBaseExt,
        MultiMetrics catTest, MultiMetrics catExt, MultiMetrics catBaseTest, MultiMetrics catBaseExt,
        BinMetrics spamTest, BinMetrics spamExt, BinMetrics spamBaseTest, BinMetrics spamBaseExt)
    {
        var inv = CultureInfo.InvariantCulture;
        Console.WriteLine();
        Console.WriteLine("Model vs baseline, test part vs external set:");
        Console.WriteLine("| Task | Metric | Test (model) | Test (baseline) | External (model) | External (baseline) |");
        Console.WriteLine("|---|---|---|---|---|---|");
        Console.WriteLine(string.Create(inv, $"| Price (non-spam) | R²   | {priceTest.R2:0.000} | {priceBaseTest.R2:0.000} | {priceExt.R2:0.000} | {priceBaseExt.R2:0.000} |"));
        Console.WriteLine(string.Create(inv, $"| Price (non-spam) | MAE (lei) | {priceTest.Mae:0.00} | {priceBaseTest.Mae:0.00} | {priceExt.Mae:0.00} | {priceBaseExt.Mae:0.00} |"));
        Console.WriteLine(string.Create(inv, $"| Price (non-spam) | MAPE | {priceTest.Mape:0.0}% | {priceBaseTest.Mape:0.0}% | {priceExt.Mape:0.0}% | {priceBaseExt.Mape:0.0}% |"));
        Console.WriteLine(string.Create(inv, $"| Category (non-spam) | Micro accuracy | {catTest.MicroAccuracy:0.000} | {catBaseTest.MicroAccuracy:0.000} | {catExt.MicroAccuracy:0.000} | {catBaseExt.MicroAccuracy:0.000} |"));
        Console.WriteLine(string.Create(inv, $"| Category (non-spam) | Macro accuracy | {catTest.MacroAccuracy:0.000} | {catBaseTest.MacroAccuracy:0.000} | {catExt.MacroAccuracy:0.000} | {catBaseExt.MacroAccuracy:0.000} |"));
        Console.WriteLine(string.Create(inv, $"| Spam (all) | Accuracy  | {spamTest.Accuracy:0.000} | {spamBaseTest.Accuracy:0.000} | {spamExt.Accuracy:0.000} | {spamBaseExt.Accuracy:0.000} |"));
        Console.WriteLine(string.Create(inv, $"| Spam (all) | Precision | {spamTest.Precision:0.000} | {spamBaseTest.Precision:0.000} | {spamExt.Precision:0.000} | {spamBaseExt.Precision:0.000} |"));
        Console.WriteLine(string.Create(inv, $"| Spam (all) | Recall    | {spamTest.Recall:0.000} | {spamBaseTest.Recall:0.000} | {spamExt.Recall:0.000} | {spamBaseExt.Recall:0.000} |"));
        Console.WriteLine(string.Create(inv, $"| Spam (all) | F1 score  | {spamTest.F1:0.000} | {spamBaseTest.F1:0.000} | {spamExt.F1:0.000} | {spamBaseExt.F1:0.000} |"));
        Console.WriteLine();
        Console.WriteLine("Price baseline: mean price of the same Category × Condition on the train part " +
            "(fallbacks: category mean, global mean).");
        Console.WriteLine("Category baseline: most frequent class on the train part.");
        Console.WriteLine("Spam baseline: hand-written rule from Baselines.cs.");
    }

    private static void PrintConfusionMatrix(Dictionary<(string Actual, string Predicted), int> matrix)
    {
        var inv = CultureInfo.InvariantCulture;
        Console.WriteLine();
        Console.WriteLine("Category confusion matrix on external (rows = true, columns = predicted):");
        var header = new StringBuilder("| true \\\\ pred |");
        var sep = new StringBuilder("|---|");
        foreach (var c in Categories)
        {
            header.Append(' ').Append(c).Append(" |");
            sep.Append("---|");
        }
        Console.WriteLine(header.ToString());
        Console.WriteLine(sep.ToString());
        foreach (var trueCat in Categories)
        {
            var sb = new StringBuilder("| ").Append(trueCat).Append(" |");
            foreach (var predCat in Categories)
            {
                var count = matrix.TryGetValue((trueCat, predCat), out var v) ? v : 0;
                sb.Append(' ').Append(count.ToString(inv)).Append(" |");
            }
            Console.WriteLine(sb.ToString());
        }
    }

    private static void PrintTopPriceErrors(List<(ListingRow Row, double Score)> preds)
    {
        var inv = CultureInfo.InvariantCulture;
        Console.WriteLine();
        Console.WriteLine("Top 10 external rows by price error:");
        Console.WriteLine("| Title | Category | True (lei) | Predicted (lei) | Error (lei) |");
        Console.WriteLine("|---|---|---|---|---|");
        var top = preds
            .Select(p => (p.Row, p.Score, Error: Math.Abs(p.Row.Price - p.Score)))
            .OrderByDescending(x => x.Error)
            .Take(10);
        foreach (var x in top)
        {
            Console.WriteLine(string.Create(inv, $"| {Truncate(x.Row.Title, 60)} | {x.Row.Category} | {x.Row.Price:0.00} | {x.Score:0.00} | {x.Error:0.00} |"));
        }
    }

    private static void PrintMisclassifiedSpam(List<(ListingRow Row, bool Predicted)> preds)
    {
        var inv = CultureInfo.InvariantCulture;
        var wrong = preds.Where(p => p.Predicted != p.Row.IsSpam).ToList();
        Console.WriteLine();
        Console.WriteLine(string.Create(inv, $"Misclassified spam rows on external: {wrong.Count}."));
        if (wrong.Count == 0) return;

        Console.WriteLine("| Title | True IsSpam | Predicted IsSpam |");
        Console.WriteLine("|---|---|---|");
        foreach (var w in wrong)
        {
            Console.WriteLine(string.Create(inv, $"| {Truncate(w.Row.Title, 80)} | {w.Row.IsSpam} | {w.Predicted} |"));
        }
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var clean = text.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
        return clean.Length <= max ? clean : clean[..(max - 1)] + "…";
    }

    // --- Scored rows ------------------------------------------------------

    private sealed class PriceScored
    {
        public float Price { get; set; }
        public float Score { get; set; }
    }

    private sealed class CategoryScored
    {
        public string Category { get; set; } = string.Empty;
        public string PredictedLabel { get; set; } = string.Empty;
    }

    private sealed class SpamScored
    {
        public bool IsSpam { get; set; }
        public bool PredictedLabel { get; set; }
    }
}
