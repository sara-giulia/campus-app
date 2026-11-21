using System.Globalization;
using System.Text;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Price regression trainer (price v2). Trains on non-spam rows only; spam
// prices are absurd on purpose and would bias the model. Features: Category
// and Condition one-hot encoded, Title and Description featurized as text.
//
// Model selection: six candidates (three algorithms x two targets) are trained
// on the train part and scored on the validation part; the test part is held
// out. Winner by MAE in lei, tie-break by R². The winner is retrained on
// train + validation and evaluated once on the test part. That is the saved
// model.
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
// train + validation of the winner (grouped by TemplateId, same non-spam
// filter) and reports R², MAE and RMSE as mean ± std.
public sealed class PriceTrainer : IModelTrainer
{
    public string Name => "Price regression (model selection on validation)";

    public string ModelFileName => "price-regression.zip";

    public TrainingResult Train(MLContext mlContext, IDataView trainData, IDataView validationData, IDataView testData)
    {
        var trainRows = NonSpamRows(mlContext, trainData);
        var validationRows = NonSpamRows(mlContext, validationData);
        var testRows = NonSpamRows(mlContext, testData);

        // 1. Six-combination search on train -> validation only.
        var candidates = new (PriceAlgorithm Alg, PriceTarget Tgt)[]
        {
            (PriceAlgorithm.FastTree, PriceTarget.Plain),
            (PriceAlgorithm.FastTree, PriceTarget.Log),
            (PriceAlgorithm.LightGbm, PriceTarget.Plain),
            (PriceAlgorithm.LightGbm, PriceTarget.Log),
            (PriceAlgorithm.Sdca, PriceTarget.Plain),
            (PriceAlgorithm.Sdca, PriceTarget.Log),
        };

        var validationResults = new List<(PriceAlgorithm Alg, PriceTarget Tgt, PriceMetrics Metrics)>();
        foreach (var (alg, tgt) in candidates)
        {
            var trainView = BuildView(mlContext, trainRows, tgt);
            var validationView = BuildView(mlContext, validationRows, tgt);
            var pipeline = BuildPipeline(mlContext, alg, tgt);
            var model = pipeline.Fit(trainView);
            var metrics = EvaluateModel(mlContext, model, validationView);
            validationResults.Add((alg, tgt, metrics));
        }

        var winner = validationResults
            .OrderBy(r => r.Metrics.Mae)
            .ThenByDescending(r => r.Metrics.R2)
            .First();

        // 2. Retrain the winner on train + validation and evaluate once on test.
        var finalPipeline = BuildPipeline(mlContext, winner.Alg, winner.Tgt);
        var trainPlusValidation = trainRows.Concat(validationRows).ToList();
        var finalFitView = BuildView(mlContext, trainPlusValidation, winner.Tgt);
        var finalModel = finalPipeline.Fit(finalFitView);

        var testView = BuildView(mlContext, testRows, winner.Tgt);
        var testMetrics = EvaluateModelDetailed(mlContext, finalModel, testView);
        var baselineMetrics = BaselinePriceDetailed(trainPlusValidation, testRows);

        // 3. Grouped 5-fold CV of the winner on train + validation, metrics in lei.
        var cv = GroupedCrossValidation(mlContext, trainPlusValidation, winner.Alg, winner.Tgt, folds: 5);

        var text = FormatMetrics(validationResults, winner, testMetrics, baselineMetrics, cv);
        var algorithm = $"{winner.Alg} / {TargetLabel(winner.Tgt)}";
        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["mae"] = testMetrics.Mae,
            ["mape"] = testMetrics.Mape,
            ["r2"] = testMetrics.R2,
            ["rmse"] = testMetrics.Rmse,
        };
        return new TrainingResult(finalModel, text, algorithm, structured);
    }

    public static IEstimator<ITransformer> BuildPipeline(MLContext mlContext, PriceAlgorithm algorithm, PriceTarget target)
    {
        IEstimator<ITransformer> pipeline =
            mlContext.Transforms.Categorical.OneHotEncoding("CategoryEncoded", nameof(TrainingRow.Category))
                .Append(mlContext.Transforms.Categorical.OneHotEncoding("ConditionEncoded", nameof(TrainingRow.Condition)))
                .Append(mlContext.Transforms.Text.FeaturizeText("TitleFeatures", nameof(TrainingRow.Title)))
                .Append(mlContext.Transforms.Text.FeaturizeText("DescriptionFeatures", nameof(TrainingRow.Description)))
                .Append(mlContext.Transforms.Concatenate("Features",
                    "CategoryEncoded", "ConditionEncoded", "TitleFeatures", "DescriptionFeatures"));

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
            .Select(r => new TrainingRow
            {
                Title = r.Title,
                Description = r.Description,
                Category = r.Category,
                Condition = r.Condition,
                Price = r.Price,
                TemplateId = r.TemplateId,
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
            var pipeline = BuildPipeline(mlContext, algorithm, target);
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

    private static string FormatMetrics(
        IReadOnlyList<(PriceAlgorithm Alg, PriceTarget Tgt, PriceMetrics Metrics)> validation,
        (PriceAlgorithm Alg, PriceTarget Tgt, PriceMetrics Metrics) winner,
        PriceMetricsDetailed test,
        PriceMetricsDetailed baseline,
        IReadOnlyList<PriceMetrics> cv)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("Validation (non-spam, 6 combinations, lei):");
        sb.AppendLine("| Algorithm | Target | R² | MAE (lei) | RMSE (lei) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in validation)
        {
            var star = r.Alg == winner.Alg && r.Tgt == winner.Tgt ? " **winner**" : string.Empty;
            sb.Append(inv, $"| {r.Alg} | {TargetLabel(r.Tgt)}{star} ");
            sb.Append(inv, $"| {r.Metrics.R2:0.000} ");
            sb.Append(inv, $"| {r.Metrics.Mae:0.00} ");
            sb.Append(inv, $"| {r.Metrics.Rmse:0.00} |");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append(inv, $"Winner: {winner.Alg} / {TargetLabel(winner.Tgt)} ");
        sb.Append(inv, $"(validation MAE {winner.Metrics.Mae:0.00} lei, R² {winner.Metrics.R2:0.000})");
        sb.AppendLine();

        sb.AppendLine();
        sb.AppendLine("Test (non-spam, winner vs mean-by-Category-x-Condition baseline, lei):");
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

        sb.AppendLine();
        sb.AppendLine("5-fold grouped CV of the winner on train + validation (by TemplateId, non-spam):");
        var r2Stats = Stats(cv.Select(m => m.R2));
        var maeStats = Stats(cv.Select(m => m.Mae));
        var rmseStats = Stats(cv.Select(m => m.Rmse));
        sb.AppendLine(inv, $"  R²:   {r2Stats.Mean:0.000} ± {r2Stats.StdDev:0.000}");
        sb.AppendLine(inv, $"  MAE:  {maeStats.Mean:0.00} ± {maeStats.StdDev:0.00} lei");
        sb.AppendLine(inv, $"  RMSE: {rmseStats.Mean:0.00} ± {rmseStats.StdDev:0.00} lei");

        if (test.Mae >= baseline.Mae)
        {
            sb.AppendLine();
            sb.AppendLine("WARNING: the winner does not beat the baseline on MAE. Stop and investigate before shipping this model (see docs/ml/price.md).");
        }

        return sb.ToString().TrimEnd();
    }

    private static (double Mean, double StdDev) Stats(IEnumerable<double> values)
    {
        var arr = values.ToArray();
        if (arr.Length == 0) return (0, 0);
        var mean = arr.Average();
        var variance = arr.Sum(v => (v - mean) * (v - mean)) / arr.Length;
        return (mean, Math.Sqrt(variance));
    }

    private static string TargetLabel(PriceTarget target) => target == PriceTarget.Log ? "log1p(Price)" : "Price";

    public sealed record PriceMetrics(double R2, double Mae, double Rmse);

    public sealed record PriceMetricsDetailed(double R2, double Mae, double Rmse, double Mape, IReadOnlyDictionary<string, double> MaePerCategory);

    private sealed class TrainingRow
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Condition { get; set; } = string.Empty;

        public float Price { get; set; }

        public float Label { get; set; }

        public string TemplateId { get; set; } = string.Empty;
    }

    private sealed class ScoredRow
    {
        public string Category { get; set; } = string.Empty;

        public float Price { get; set; }

        public float Score { get; set; }
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
