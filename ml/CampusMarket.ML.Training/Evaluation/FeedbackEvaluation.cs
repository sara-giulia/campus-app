using System.Globalization;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CampusMarket.ML.Training.Evaluation;

// Feedback evaluation: the SAVED price model (ml/models/price-regression.zip)
// on ml/data/feedback.csv, the real sales exported by
// GET /api/v1/admin/ml/price-feedback (label = SoldPrice). Evaluation only:
// the rows never enter training. Reuses the prediction and metric code of
// ExternalEvaluation. The CSV has no SuggestedPrice column, so the
// suggester-vs-sold MAE is not reported here (it lives on the
// /admin/ml/price-accuracy endpoint).
//
// Called from Program.cs after the external evaluation; no-op when feedback.csv is missing.
public static class FeedbackEvaluation
{
    public static void Run(MLContext mlContext, string feedbackCsvPath, string modelsDir)
    {
        if (!File.Exists(feedbackCsvPath))
        {
            return;
        }

        // ReadMultilines: the export keeps new lines of listing descriptions inside quotes.
        var feedbackData = mlContext.Data.LoadFromTextFile<ListingRow>(
            feedbackCsvPath,
            new TextLoader.Options
            {
                HasHeader = true,
                Separators = [','],
                AllowQuoting = true,
                ReadMultilines = true,
            });

        var rows = mlContext.Data.CreateEnumerable<ListingRow>(feedbackData, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .ToList();

        var inv = CultureInfo.InvariantCulture;
        Console.WriteLine();
        Console.WriteLine("=== Feedback evaluation (ml/data/feedback.csv) ===");
        Console.WriteLine();

        if (rows.Count == 0)
        {
            Console.WriteLine("Feedback rows: 0, nothing to evaluate.");
            return;
        }

        var priceModel = mlContext.Model.Load(Path.Combine(modelsDir, "price-regression.zip"), out _);
        var metrics = ExternalEvaluation.RegressionMetrics(
            ExternalEvaluation.PricePredictions(mlContext, priceModel, rows));

        Console.WriteLine("Saved price model on real sales (label = SoldPrice):");
        Console.WriteLine("| n | MAE (lei) | R² |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine(string.Create(inv, $"| {rows.Count} | {metrics.Mae:0.00} | {metrics.R2:0.000} |"));
        Console.WriteLine(string.Create(inv,
            $"Indicative only: n = {rows.Count}; with few sales MAE and especially R² move a lot from one sale to the next."));
    }
}
