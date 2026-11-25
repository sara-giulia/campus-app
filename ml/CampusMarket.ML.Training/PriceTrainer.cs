using System.Globalization;
using System.Text;
using CampusMarket.Api.Infrastructure.ML;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Price regression trainer (price v2). Trains on non-spam rows only; spam
// prices are absurd on purpose and would bias the model.
//
// Two feature sets:
//   - Current: Category and Condition one-hot encoded, Title and Description
//     featurized as text. Exactly the pipeline of the first price v2.
//   - Text: Current plus four explicit features read from title + description by
//     PriceTextFeatures (the same file the API uses): OneHot(Brand), StorageGb,
//     CpuTier, IsNewOrSealed.
//
// Model selection: twelve candidates (two feature sets x three algorithms x two
// targets) are trained on the train part and scored on the validation part; the
// test part is held out. Winner by MAE in lei, tie-break by R². Each feature set's
// best candidate is retrained on train + validation, evaluated once on the test
// part and cross-validated (5-fold grouped); the metrics text compares the two.
// The overall winner is the saved model, whichever feature set it comes from.
//
// Seeds: every component created on an MLContext draws a seed from it, and the
// price trainer runs first, so extra work on the shared context would change the
// category, spam and similar models trained after it. The Current feature set
// therefore runs on the shared context with exactly the calls it always made, and
// everything Text-related runs on a private MLContext(seed: DatasetSplit.Seed).
//
// Target choice: when Target.Log is picked the regressor learns log1p(Price)
// and its raw Score is in log space. The pipeline then appends an Expression
// transform that undoes the log (Score = exp(ScoreLog) - 1), so the saved
// model always exposes a Score column in lei. The API's PriceSuggester can
// keep reading Score without knowing which target won; the Expression
// transform is a built-in ML.NET transform and needs no custom registration
// on the load side.
//
// Metrics (R², MAE, RMSE, MAPE %, MAE per category) are always computed in
// lei: the pipeline's Score column already is, whatever the target. On top of
// the single hold-out, the trainer runs a 5-fold grouped cross-validation on
// train + validation (grouped by TemplateId, same non-spam filter) and reports
// R², MAE and RMSE as mean ± std.
public sealed class PriceTrainer : IModelTrainer
{
    public string Name => "Price regression (model selection on validation)";

    public string ModelFileName => "price-regression.zip";

    // Both retrained finalists of the last Train call, for the external evaluation.
    public PriceComparison? Comparison { get; private set; }

    public TrainingResult Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData)
    {
        var trainRows = NonSpamRows(mlContext, trainData);
        var validationRows = NonSpamRows(mlContext, validationData);
        var testRows = NonSpamRows(mlContext, testData);

        // Current runs on the shared context, Text on a private one (see the header).
        var current = RunFeatureSet(mlContext, PriceFeatureSet.Current, trainRows, validationRows, testRows);
        var text = RunFeatureSet(new MLContext(seed: DatasetSplit.Seed), PriceFeatureSet.Text, trainRows, validationRows, testRows);

        // Same winner rule over all twelve candidates: MAE on validation, then R².
        var winnerRun = new[] { current, text }
            .OrderBy(r => r.Best.Metrics.Mae)
            .ThenByDescending(r => r.Best.Metrics.R2)
            .First();

        Comparison = new PriceComparison(current.Model, text.Model, winnerRun.Set);

        var text12 = FormatMetrics(current, text, winnerRun);
        var winner = winnerRun.Best;
        var algorithm = $"{winner.Alg} / {TargetLabel(winner.Tgt)} / {FeatureSetLabel(winner.Set)} features";
        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["mae"] = winnerRun.Test.Mae,
            ["mape"] = winnerRun.Test.Mape,
            ["r2"] = winnerRun.Test.R2,
            ["rmse"] = winnerRun.Test.Rmse,
        };
        return new TrainingResult(winnerRun.Model, text12, algorithm, structured);
    }

    private static FeatureSetRun RunFeatureSet(
        MLContext ctx,
        PriceFeatureSet set,
        IReadOnlyList<TrainingRow> trainRows,
        IReadOnlyList<TrainingRow> validationRows,
        IReadOnlyList<TrainingRow> testRows)
    {
        // 1. Six-combination search on train -> validation only.
        var combinations = new (PriceAlgorithm Alg, PriceTarget Tgt)[]
        {
            (PriceAlgorithm.FastTree, PriceTarget.Plain),
            (PriceAlgorithm.FastTree, PriceTarget.Log),
            (PriceAlgorithm.LightGbm, PriceTarget.Plain),
            (PriceAlgorithm.LightGbm, PriceTarget.Log),
            (PriceAlgorithm.Sdca, PriceTarget.Plain),
            (PriceAlgorithm.Sdca, PriceTarget.Log),
        };

        var candidates = new List<Candidate>();
        foreach (var (alg, tgt) in combinations)
        {
            var trainView = BuildView(ctx, trainRows, tgt);
            var validationView = BuildView(ctx, validationRows, tgt);
            var pipeline = BuildPipeline(ctx, alg, tgt, set);
            var model = pipeline.Fit(trainView);
            candidates.Add(new Candidate(set, alg, tgt, EvaluateModel(ctx, model, validationView)));
        }

        var best = candidates
            .OrderBy(r => r.Metrics.Mae)
            .ThenByDescending(r => r.Metrics.R2)
            .First();

        // 2. Retrain the best candidate on train + validation and evaluate once on test.
        var finalPipeline = BuildPipeline(ctx, best.Alg, best.Tgt, set);
        var trainPlusValidation = trainRows.Concat(validationRows).ToList();
        var finalFitView = BuildView(ctx, trainPlusValidation, best.Tgt);
        var finalModel = finalPipeline.Fit(finalFitView);

        var testView = BuildView(ctx, testRows, best.Tgt);
        var testMetrics = EvaluateModelDetailed(ctx, finalModel, testView);
        var baselineMetrics = BaselinePriceDetailed(trainPlusValidation, testRows);

        // 3. Grouped 5-fold CV of the best candidate on train + validation, metrics in lei.
        var cv = GroupedCrossValidation(ctx, trainPlusValidation, best.Alg, best.Tgt, set, folds: 5);

        return new FeatureSetRun(set, candidates, best, finalModel, testMetrics, baselineMetrics, cv);
    }

    public static IEstimator<ITransformer> BuildPipeline(
        MLContext mlContext,
        PriceAlgorithm algorithm,
        PriceTarget target,
        PriceFeatureSet featureSet = PriceFeatureSet.Current)
    {
        IEstimator<ITransformer> pipeline =
            mlContext.Transforms.Categorical.OneHotEncoding("CategoryEncoded", nameof(TrainingRow.Category))
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("ConditionEncoded", nameof(TrainingRow.Condition)))
                .Append(mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(TrainingRow.Title)))
                .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(TrainingRow.Description)));

        if (featureSet == PriceFeatureSet.Text)
        {
            pipeline = pipeline
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("BrandEncoded", nameof(TrainingRow.Brand)))
                .Append(mlContext.Transforms.Concatenate("Features",
                    "CategoryEncoded", "ConditionEncoded", "TitleFeatures", "DescriptionFeatures",
                    "BrandEncoded", nameof(TrainingRow.StorageGb), nameof(TrainingRow.CpuTier), nameof(TrainingRow.IsNewOrSealed)));
        }
        else
        {
            pipeline = pipeline.Append(mlContext.Transforms.Concatenate("Features",
                "CategoryEncoded", "ConditionEncoded", "TitleFeatures", "DescriptionFeatures"));
        }

        pipeline = algorithm switch
        {
            PriceAlgorithm.FastTree => pipeline.Append(
                mlContext.Regression.Trainers.FastTree(new Microsoft.ML.Trainers.FastTree.FastTreeRegressionTrainer.Options
                {
                    LabelColumnName = "Label",
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1,
                })),
            PriceAlgorithm.LightGbm => pipeline.Append(
                mlContext.Regression.Trainers.LightGbm(new Microsoft.ML.Trainers.LightGbm.LightGbmRegressionTrainer.Options
                {
                    LabelColumnName = "Label",
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1,
                })),
            PriceAlgorithm.Sdca => pipeline.Append(
                mlContext.Regression.Trainers.Sdca(new Microsoft.ML.Trainers.SdcaRegressionTrainer.Options
                {
                    LabelColumnName = "Label",
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1,
                })),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown price algorithm."),
        };

        if (target == PriceTarget.Log)
        {
            // Rename the regressor's Score (log space) to ScoreLog, then rewrite
            // Score = exp(ScoreLog) - 1 so the saved model's Score is in lei.
            pipeline = pipeline
                .Append(mlContext.Transforms.CopyColumns("ScoreLog", "Score"))
                .Append(mlContext.Transforms.Expression("Score", "x : exp(x) - 1", "ScoreLog"));
        }

        return pipeline;
    }

    private static List<TrainingRow> NonSpamRows(MLContext mlContext, IDataView data) =>
        mlContext.Data.CreateEnumerable<ListingRow>(data, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .Select(r =>
            {
                var f = PriceTextFeatures.Extract(r.Title, r.Description);
                return new TrainingRow
                {
                    Title = r.Title,
                    Description = r.Description,
                    Category = r.Category,
                    Condition = r.Condition,
                    Price = r.Price,
                    TemplateId = r.TemplateId,
                    Brand = f.Brand,
                    StorageGb = f.StorageGb,
                    CpuTier = f.CpuTier,
                    IsNewOrSealed = f.IsNewOrSealed,
                };
            })
            .ToList();

    private static IDataView BuildView(MLContext mlContext, IReadOnlyList<TrainingRow> rows, PriceTarget target)
    {
        var withLabel = rows.Select(r => new TrainingRow
        {
            Title = r.Title,
            Description = r.Description,
            Category = r.Category,
            Condition = r.Condition,
            Price = r.Price,
            TemplateId = r.TemplateId,
            Brand = r.Brand,
            StorageGb = r.StorageGb,
            CpuTier = r.CpuTier,
            IsNewOrSealed = r.IsNewOrSealed,
            Label = target == PriceTarget.Log ? MathF.Log(r.Price + 1f) : r.Price,
        }).ToList();
        return mlContext.Data.LoadFromEnumerable(withLabel);
    }

    private static PriceMetrics EvaluateModel(MLContext mlContext, ITransformer model, IDataView view)
    {
        var rows = mlContext.Data.CreateEnumerable<ScoredRow>(model.Transform(view), reuseRowObject: false).ToList();
        var actual = rows.Select(r => (double)r.Price).ToArray();
        var predicted = rows.Select(r => Math.Max(0.0, r.Score)).ToArray();
        return Regression(actual, predicted);
    }

    private static PriceMetricsDetailed EvaluateModelDetailed(MLContext mlContext, ITransformer model, IDataView view)
    {
        var rows = mlContext.Data.CreateEnumerable<ScoredRow>(model.Transform(view), reuseRowObject: false).ToList();

        var actual = rows.Select(r => (double)r.Price).ToArray();
        var predicted = rows.Select(r => Math.Max(0.0, r.Score)).ToArray();
        var basic = Regression(actual, predicted);
        var mape = MeanAbsolutePercentageError(actual, predicted);

        var perCategory = rows
            .GroupBy(r => r.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Average(r => Math.Abs((double)r.Price - Math.Max(0.0, r.Score))));

        return new PriceMetricsDetailed(basic.R2, basic.Mae, basic.Rmse, mape, perCategory);
    }

    private static PriceMetricsDetailed BaselinePriceDetailed(IReadOnlyList<TrainingRow> trainRows, IReadOnlyList<TrainingRow> testRows)
    {
        var catCondMean = trainRows
            .GroupBy(r => (r.Category, r.Condition))
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Price));

        var catMean = trainRows
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Price));

        var globalMean = trainRows.Average(r => (double)r.Price);

        double Predict(TrainingRow r)
        {
            if (catCondMean.TryGetValue((r.Category, r.Condition), out var v)) return v;
            if (catMean.TryGetValue(r.Category, out var cv)) return cv;
            return globalMean;
        }

        var actual = testRows.Select(r => (double)r.Price).ToArray();
        var predicted = testRows.Select(Predict).ToArray();
        var basic = Regression(actual, predicted);
        var mape = MeanAbsolutePercentageError(actual, predicted);

        var perCategory = testRows
            .Select((r, i) => (r.Category, Error: Math.Abs((double)r.Price - predicted[i])))
            .GroupBy(x => x.Category)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Error));

        return new PriceMetricsDetailed(basic.R2, basic.Mae, basic.Rmse, mape, perCategory);
    }

    private static List<PriceMetrics> GroupedCrossValidation(
        MLContext mlContext,
        IReadOnlyList<TrainingRow> rows,
        PriceAlgorithm algorithm,
        PriceTarget target,
        PriceFeatureSet featureSet,
        int folds)
    {
        var templateIds = rows.Select(r => r.TemplateId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
        var random = new Random(DatasetSplit.Seed); // NOSONAR: deterministic seeded shuffle for reproducible cross-validation, not security
        for (var i = templateIds.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (templateIds[i], templateIds[j]) = (templateIds[j], templateIds[i]);
        }

        var assignment = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < templateIds.Count; i++)
        {
            assignment[templateIds[i]] = i % folds;
        }

        var results = new List<PriceMetrics>(folds);
        for (var k = 0; k < folds; k++)
        {
            var trainFold = rows.Where(r => assignment[r.TemplateId] != k).ToList();
            var testFold = rows.Where(r => assignment[r.TemplateId] == k).ToList();
            if (testFold.Count == 0)
            {
                continue;
            }

            var trainView = BuildView(mlContext, trainFold, target);
            var testView = BuildView(mlContext, testFold, target);
            var pipeline = BuildPipeline(mlContext, algorithm, target, featureSet);
            var model = pipeline.Fit(trainView);
            results.Add(EvaluateModel(mlContext, model, testView));
        }
        return results;
    }

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

    private static double MeanAbsolutePercentageError(double[] actual, double[] predicted)
    {
        double sum = 0;
        var count = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            if (actual[i] <= 0) continue;
            sum += Math.Abs(actual[i] - predicted[i]) / actual[i];
            count++;
        }
        return count > 0 ? 100.0 * sum / count : 0;
    }

    private static string FormatMetrics(FeatureSetRun current, FeatureSetRun text, FeatureSetRun winnerRun)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        var winner = winnerRun.Best;

        sb.AppendLine("Validation (non-spam, 12 candidates = 2 feature sets x 3 algorithms x 2 targets, lei):");
        sb.AppendLine("| Features | Algorithm | Target | R² | MAE (lei) | RMSE (lei) |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var r in current.Candidates.Concat(text.Candidates))
        {
            var star = r == winner ? " **winner**" : string.Empty;
            sb.Append(inv, $"| {FeatureSetLabel(r.Set)} | {r.Alg} | {TargetLabel(r.Tgt)}{star} ");
            sb.Append(inv, $"| {r.Metrics.R2:0.000} ");
            sb.Append(inv, $"| {r.Metrics.Mae:0.00} ");
            sb.Append(inv, $"| {r.Metrics.Rmse:0.00} |");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append(inv, $"Winner: {winner.Alg} / {TargetLabel(winner.Tgt)} / {FeatureSetLabel(winner.Set)} features ");
        sb.Append(inv, $"(validation MAE {winner.Metrics.Mae:0.00} lei, R² {winner.Metrics.R2:0.000})");
        sb.AppendLine();
        sb.AppendLine(winner.Set == PriceFeatureSet.Current
            ? "The Current feature set won: the four explicit text features did not beat today's pipeline on validation, so the saved model does not use them."
            : "The Text feature set won: the explicit text features beat today's pipeline on validation, so the saved model uses them.");

        var cur = current.Best;
        var txt = text.Best;
        var curCv = Summarize(current.Cv);
        var txtCv = Summarize(text.Cv);
        sb.AppendLine();
        sb.AppendLine("Best Current vs best Text (each best candidate retrained on train + validation for test and CV; lei):");
        sb.AppendLine("| Metric | Current | Text |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine(inv, $"| Best candidate | {cur.Alg} / {TargetLabel(cur.Tgt)} | {txt.Alg} / {TargetLabel(txt.Tgt)} |");
        sb.AppendLine(inv, $"| Validation MAE | {cur.Metrics.Mae:0.00} lei | {txt.Metrics.Mae:0.00} lei |");
        sb.AppendLine(inv, $"| Validation R² | {cur.Metrics.R2:0.000} | {txt.Metrics.R2:0.000} |");
        sb.AppendLine(inv, $"| Test MAE | {current.Test.Mae:0.00} lei | {text.Test.Mae:0.00} lei |");
        sb.AppendLine(inv, $"| Test R² | {current.Test.R2:0.000} | {text.Test.R2:0.000} |");
        sb.AppendLine(inv, $"| Test RMSE | {current.Test.Rmse:0.00} lei | {text.Test.Rmse:0.00} lei |");
        sb.AppendLine(inv, $"| Test MAPE | {current.Test.Mape:0.0}% | {text.Test.Mape:0.0}% |");
        sb.AppendLine(inv, $"| 5-fold CV MAE | {curCv.Mae.Mean:0.00} ± {curCv.Mae.StdDev:0.00} lei | {txtCv.Mae.Mean:0.00} ± {txtCv.Mae.StdDev:0.00} lei |");
        sb.AppendLine(inv, $"| 5-fold CV R² | {curCv.R2.Mean:0.000} ± {curCv.R2.StdDev:0.000} | {txtCv.R2.Mean:0.000} ± {txtCv.R2.StdDev:0.000} |");
        sb.AppendLine(inv, $"| 5-fold CV RMSE | {curCv.Rmse.Mean:0.00} ± {curCv.Rmse.StdDev:0.00} lei | {txtCv.Rmse.Mean:0.00} ± {txtCv.Rmse.StdDev:0.00} lei |");

        var test = winnerRun.Test;
        var baseline = winnerRun.Baseline;
        sb.AppendLine();
        sb.AppendLine(inv, $"Test (non-spam, saved winner [{FeatureSetLabel(winner.Set)}] vs mean-by-Category-x-Condition baseline, lei):");
        sb.AppendLine("| Metric | Winner | Baseline |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine(inv, $"| R²   | {test.R2:0.000} | {baseline.R2:0.000} |");
        sb.AppendLine(inv, $"| MAE  | {test.Mae:0.00} lei | {baseline.Mae:0.00} lei |");
        sb.AppendLine(inv, $"| RMSE | {test.Rmse:0.00} lei | {baseline.Rmse:0.00} lei |");
        sb.AppendLine(inv, $"| MAPE | {test.Mape:0.0}% | {baseline.Mape:0.0}% |");

        sb.AppendLine();
        sb.AppendLine("MAE per category on test (lei):");
        sb.AppendLine("| Category | Winner | Baseline |");
        sb.AppendLine("|---|---|---|");
        var categories = test.MaePerCategory.Keys
            .Union(baseline.MaePerCategory.Keys)
            .OrderBy(c => c, StringComparer.Ordinal);
        foreach (var cat in categories)
        {
            var winnerMae = test.MaePerCategory.TryGetValue(cat, out var wv) ? wv.ToString("0.00", inv) + " lei" : "–";
            var baselineMae = baseline.MaePerCategory.TryGetValue(cat, out var bv) ? bv.ToString("0.00", inv) + " lei" : "–";
            sb.AppendLine(inv, $"| {cat} | {winnerMae} | {baselineMae} |");
        }

        var winnerCv = winnerRun.Set == PriceFeatureSet.Current ? curCv : txtCv;
        sb.AppendLine();
        sb.AppendLine("5-fold grouped CV of the winner on train + validation (by TemplateId, non-spam):");
        sb.AppendLine(inv, $"  R²:   {winnerCv.R2.Mean:0.000} ± {winnerCv.R2.StdDev:0.000}");
        sb.AppendLine(inv, $"  MAE:  {winnerCv.Mae.Mean:0.00} ± {winnerCv.Mae.StdDev:0.00} lei");
        sb.AppendLine(inv, $"  RMSE: {winnerCv.Rmse.Mean:0.00} ± {winnerCv.Rmse.StdDev:0.00} lei");

        if (test.Mae >= baseline.Mae)
        {
            sb.AppendLine();
            sb.AppendLine("WARNING: the winner does not beat the baseline on MAE. Stop and investigate before shipping this model (see docs/ml/price.md).");
        }

        return sb.ToString().TrimEnd();
    }

    private static CvSummary Summarize(IReadOnlyList<PriceMetrics> cv) =>
        new(Stats(cv.Select(m => m.R2)), Stats(cv.Select(m => m.Mae)), Stats(cv.Select(m => m.Rmse)));

    private static (double Mean, double StdDev) Stats(IEnumerable<double> values)
    {
        var arr = values.ToArray();
        if (arr.Length == 0) return (0, 0);
        var mean = arr.Average();
        var variance = arr.Sum(v => (v - mean) * (v - mean)) / arr.Length;
        return (mean, Math.Sqrt(variance));
    }

    private static string TargetLabel(PriceTarget target) => target == PriceTarget.Log ? "log1p(Price)" : "Price";

    private static string FeatureSetLabel(PriceFeatureSet set) => set == PriceFeatureSet.Text ? "Text" : "Current";

    public sealed record PriceMetrics(double R2, double Mae, double Rmse);

    public sealed record PriceMetricsDetailed(double R2, double Mae, double Rmse, double Mape, IReadOnlyDictionary<string, double> MaePerCategory);

    // The two retrained finalists, so the external evaluation can score both on external.csv.
    public sealed record PriceComparison(ITransformer CurrentModel, ITransformer TextModel, PriceFeatureSet Winner);

    private sealed record Candidate(PriceFeatureSet Set, PriceAlgorithm Alg, PriceTarget Tgt, PriceMetrics Metrics);

    private sealed record FeatureSetRun(
        PriceFeatureSet Set,
        IReadOnlyList<Candidate> Candidates,
        Candidate Best,
        ITransformer Model,
        PriceMetricsDetailed Test,
        PriceMetricsDetailed Baseline,
        IReadOnlyList<PriceMetrics> Cv);

    private sealed record CvSummary((double Mean, double StdDev) R2, (double Mean, double StdDev) Mae, (double Mean, double StdDev) Rmse);

    private sealed class TrainingRow
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Condition { get; set; } = string.Empty;

        public float Price { get; set; }

        public float Label { get; set; }

        public string TemplateId { get; set; } = string.Empty;

        public string Brand { get; set; } = PriceTextFeatures.NoBrand;

        public float StorageGb { get; set; }

        public float CpuTier { get; set; }

        public float IsNewOrSealed { get; set; }
    }

    private sealed class ScoredRow
    {
        public string Category { get; set; } = string.Empty;

        public float Price { get; set; }

        public float Score { get; set; }
    }
}

// What a price model scores: the four columns of a listing plus the four text features.
// Used wherever a saved price model is scored from ListingRow data (baselines, external set),
// because a model trained on the Text feature set needs the extra columns at Transform time.
public sealed class PriceModelInput
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;

    public float Price { get; set; }

    public string Brand { get; set; } = PriceTextFeatures.NoBrand;

    public float StorageGb { get; set; }

    public float CpuTier { get; set; }

    public float IsNewOrSealed { get; set; }

    public static PriceModelInput From(ListingRow row)
    {
        var f = PriceTextFeatures.Extract(row.Title, row.Description);
        return new PriceModelInput
        {
            Title = row.Title,
            Description = row.Description,
            Category = row.Category,
            Condition = row.Condition,
            Price = row.Price,
            Brand = f.Brand,
            StorageGb = f.StorageGb,
            CpuTier = f.CpuTier,
            IsNewOrSealed = f.IsNewOrSealed,
        };
    }
}

public enum PriceAlgorithm
{
    FastTree,
    LightGbm,
    Sdca,
}

public enum PriceTarget
{
    Plain,
    Log,
}

public enum PriceFeatureSet
{
    Current,
    Text,
}
