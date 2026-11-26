using System.Globalization;
using CampusMarket.ML.Training;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

var mlContext = new MLContext(seed: DatasetSplit.Seed);

var mlDir = ResolveMlDirectory();
var csvPath = Path.Combine(mlDir, "data", "listings.csv");
var modelsDir = Path.Combine(mlDir, "models");
Directory.CreateDirectory(modelsDir);

// "--messages" trains only the spam model of the chat messages, so the four listing models and their
// .json files are not rewritten when only that one changes.
if (args.Contains("--messages"))
{
    MessageSpamTraining.Run(mlDir, modelsDir);
    return;
}

Console.WriteLine($"Loading dataset from {csvPath}");

var data = mlContext.Data.LoadFromTextFile<ListingRow>(
    csvPath,
    hasHeader: true,
    separatorChar: ',',
    allowQuoting: true);

var (trainSet, validationSet, testSet) = DatasetSplit.Split(mlContext, data);

var trainCount = Count(trainSet);
var validationCount = Count(validationSet);
var testCount = Count(testSet);
var total = trainCount + validationCount + testCount;

var trainTemplates = DistinctTemplateIds(trainSet);
var validationTemplates = DistinctTemplateIds(validationSet);
var testTemplates = DistinctTemplateIds(testSet);

Console.WriteLine($"Train rows:      {trainCount,5} ({Pct(trainCount, total)}, {trainTemplates.Count} TemplateIds)");
Console.WriteLine($"Validation rows: {validationCount,5} ({Pct(validationCount, total)}, {validationTemplates.Count} TemplateIds)");
Console.WriteLine($"Test rows:       {testCount,5} ({Pct(testCount, total)}, {testTemplates.Count} TemplateIds)");

var trainValOverlap = trainTemplates.Intersect(validationTemplates).Count();
var trainTestOverlap = trainTemplates.Intersect(testTemplates).Count();
var valTestOverlap = validationTemplates.Intersect(testTemplates).Count();
Console.WriteLine($"TemplateId overlap train∩validation: {trainValOverlap}");
Console.WriteLine($"TemplateId overlap train∩test:       {trainTestOverlap}");
Console.WriteLine($"TemplateId overlap validation∩test:  {valTestOverlap}");

CheckProportion("train", trainCount, total, 0.70);
CheckProportion("validation", validationCount, total, 0.15);
CheckProportion("test", testCount, total, 0.15);

var priceTrainer = new PriceTrainer();
var trainers = new List<IModelTrainer>
{
    priceTrainer,
    new CategoryTrainer(),
    new SpamTrainer(),
    new SimilarTrainer()
};

var trainedModels = new Dictionary<string, ITransformer>();
var trainedSettings = new Dictionary<string, IReadOnlyDictionary<string, double>?>();

foreach (var trainer in trainers)
{
    Console.WriteLine();
    Console.WriteLine($"=== {trainer.Name} ===");

    var result = trainer.Train(mlContext, trainSet, validationSet, testSet);

    Console.WriteLine(result.MetricsText);

    var modelPath = Path.Combine(modelsDir, trainer.ModelFileName);
    mlContext.Model.Save(result.Model, data.Schema, modelPath);
    Console.WriteLine($"Saved model to {modelPath}");

    ModelMetadata.Save(
        modelFilePath: modelPath,
        modelName: trainer.Name,
        algorithm: result.Algorithm,
        testMetrics: result.TestMetrics,
        csvPath: csvPath,
        trainRowCount: trainCount,
        validationRowCount: validationCount,
        testRowCount: testCount,
        settings: result.Settings);
    Console.WriteLine($"Saved metadata to {Path.ChangeExtension(modelPath, ".json")}");

    trainedModels[trainer.ModelFileName] = result.Model;
    trainedSettings[trainer.ModelFileName] = result.Settings;
}

Console.WriteLine();
Console.WriteLine("=== Baselines vs models (same test part) ===");

var priceModel = trainedModels["price-regression.zip"];
var categoryModel = trainedModels["category-classification.zip"];
var spamModel = trainedModels["spam-detection.zip"];

var priceModelMetrics = Baselines.EvaluatePriceModel(mlContext, priceModel, testSet);
var priceBaseline = Baselines.Price(mlContext, trainSet, testSet);

var categoryModelMetrics = Baselines.EvaluateCategoryModel(mlContext, categoryModel, testSet);
var categoryMostFrequent = Baselines.CategoryMostFrequent(mlContext, trainSet, testSet);
var categoryKeywords = Baselines.CategoryKeywords(mlContext, testSet);

var spamModelMetrics = Baselines.EvaluateSpamModel(mlContext, spamModel, testSet);
var spamBaseline = Baselines.Spam(mlContext, testSet);

Console.WriteLine();
Console.WriteLine("Price (non-spam, test):");
Console.WriteLine("| Metric | Model (saved winner) | Baseline (mean by Category x Condition) |");
Console.WriteLine("|---|---|---|");
Console.WriteLine($"| R²   | {F3(priceModelMetrics.R2)} | {F3(priceBaseline.R2)} |");
Console.WriteLine($"| MAE  | {F2(priceModelMetrics.Mae)} lei | {F2(priceBaseline.Mae)} lei |");
Console.WriteLine($"| RMSE | {F2(priceModelMetrics.Rmse)} lei | {F2(priceBaseline.Rmse)} lei |");

Console.WriteLine();
Console.WriteLine("Category (non-spam, test):");
Console.WriteLine("| Metric | Model (SDCA max entropy) | Most frequent class | Keyword rule |");
Console.WriteLine("|---|---|---|---|");
Console.WriteLine($"| Micro accuracy | {F3(categoryModelMetrics.MicroAccuracy)} | {F3(categoryMostFrequent.MicroAccuracy)} | {F3(categoryKeywords.MicroAccuracy)} |");
Console.WriteLine($"| Macro accuracy | {F3(categoryModelMetrics.MacroAccuracy)} | {F3(categoryMostFrequent.MacroAccuracy)} | {F3(categoryKeywords.MacroAccuracy)} |");

Console.WriteLine();
Console.WriteLine("Spam (all rows, test):");
Console.WriteLine("| Metric | Model (SDCA logistic) | Rule baseline |");
Console.WriteLine("|---|---|---|");
Console.WriteLine($"| Accuracy  | {F3(spamModelMetrics.Accuracy)} | {F3(spamBaseline.Accuracy)} |");
Console.WriteLine($"| Precision | {F3(spamModelMetrics.Precision)} | {F3(spamBaseline.Precision)} |");
Console.WriteLine($"| Recall    | {F3(spamModelMetrics.Recall)} | {F3(spamBaseline.Recall)} |");
Console.WriteLine($"| F1 score  | {F3(spamModelMetrics.F1)} | {F3(spamBaseline.F1)} |");

var externalCsvPath = Path.Combine(mlDir, "data", "external.csv");
ExternalEvaluation.Run(mlContext, externalCsvPath, modelsDir, trainSet, testSet, priceTrainer.Comparison);

var feedbackCsvPath = Path.Combine(mlDir, "data", "feedback.csv");
FeedbackEvaluation.Run(mlContext, feedbackCsvPath, modelsDir);

// Error analysis for the model cards: the mistakes worth reading, on test and on the external set,
// with the thresholds chosen on validation.
var minConfidence = trainedSettings["category-classification.zip"]!["minConfidence"];
var spamThreshold = trainedSettings["spam-detection.zip"]!["threshold"];
var testRows = mlContext.Data.CreateEnumerable<ListingRow>(testSet, reuseRowObject: false).ToList();
var externalRows = File.Exists(externalCsvPath)
    ? mlContext.Data.CreateEnumerable<ListingRow>(
        mlContext.Data.LoadFromTextFile<ListingRow>(externalCsvPath, hasHeader: true, separatorChar: ',', allowQuoting: true),
        reuseRowObject: false).ToList()
    : [];

Console.WriteLine();
Console.WriteLine("=== Error analysis ===");
foreach (var (part, rows) in new[] { ("test part", testRows), ("external set", externalRows) })
{
    if (rows.Count == 0)
        continue;

    Console.WriteLine();
    Console.WriteLine($"Category, most confident mistakes on the {part} (shown by the API when confidence >= {F2(minConfidence)}):");
    Console.WriteLine(ErrorAnalysis.ToMarkdown(ErrorAnalysis.CategoryMistakes(mlContext, categoryModel, rows)));
    Console.WriteLine();
    Console.WriteLine($"Spam mistakes on the {part}:");
    Console.WriteLine(ErrorAnalysis.ToMarkdown(ErrorAnalysis.SpamMistakesOf(mlContext, spamModel, rows, spamThreshold), spamThreshold));
}

MessageSpamTraining.Run(mlDir, modelsDir);

static string F3(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
static string F2(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

int Count(IDataView view) =>
    mlContext.Data.CreateEnumerable<ListingRow>(view, reuseRowObject: false).Count();

HashSet<string> DistinctTemplateIds(IDataView view) =>
    mlContext.Data.CreateEnumerable<ListingRow>(view, reuseRowObject: false)
        .Select(r => r.TemplateId)
        .ToHashSet();

static string Pct(int count, int total) =>
    (100.0 * count / total).ToString("0.0", CultureInfo.InvariantCulture) + "%";

static void CheckProportion(string name, int count, int total, double target)
{
    var actual = (double)count / total;
    var driftPp = Math.Abs(actual - target) * 100;
    if (driftPp > 5.0)
    {
        var msg =
            $"Part '{name}' is {actual * 100:F1}% of the dataset, which is {driftPp:F1} pp away " +
            $"from the 70/15/15 target of {target * 100:F0}%. Stopping before training because the " +
            $"group split has drifted too far — adjust the dataset or the TemplateId assignment.";
        Console.Error.WriteLine(msg);
        throw new InvalidOperationException(msg);
    }
}

static string ResolveMlDirectory()
{
    var baseDir = AppContext.BaseDirectory;
    var dir = new DirectoryInfo(baseDir);
    while (dir is not null && dir.Name != "CampusMarket.ML.Training")
    {
        dir = dir.Parent;
    }
    if (dir?.Parent is null)
    {
        throw new InvalidOperationException("Could not locate the ml/ directory from " + baseDir);
    }
    return dir.Parent.FullName;
}
