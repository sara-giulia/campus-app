using System.Globalization;
using CampusMarket.ML.Training;
using Microsoft.ML;

var mlContext = new MLContext(seed: DatasetSplit.Seed);

var mlDir = ResolveMlDirectory();
var csvPath = Path.Combine(mlDir, "data", "listings.csv");
var modelsDir = Path.Combine(mlDir, "models");
Directory.CreateDirectory(modelsDir);

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

var trainers = new List<IModelTrainer>
{
    new PriceTrainer(),
    new CategoryTrainer(),
    new SpamTrainer()
};

foreach (var trainer in trainers)
{
    Console.WriteLine();
    Console.WriteLine($"=== {trainer.Name} ===");

    var (model, metrics) = trainer.Train(mlContext, trainSet, validationSet, testSet);

    Console.WriteLine(metrics);

    var modelPath = Path.Combine(modelsDir, trainer.ModelFileName);
    mlContext.Model.Save(model, data.Schema, modelPath);
    Console.WriteLine($"Saved model to {modelPath}");
}

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
