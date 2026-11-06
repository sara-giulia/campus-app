using CampusMarket.ML.Training;
using Microsoft.ML;

var mlContext = new MLContext(seed: 42);

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

var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

var trainCount = mlContext.Data.CreateEnumerable<ListingRow>(split.TrainSet, reuseRowObject: false).Count();
var testCount = mlContext.Data.CreateEnumerable<ListingRow>(split.TestSet, reuseRowObject: false).Count();

Console.WriteLine($"Train rows: {trainCount}");
Console.WriteLine($"Test rows:  {testCount}");

var trainers = new List<IModelTrainer>
{
    new PriceTrainer(),
    new CategoryTrainer()
};

foreach (var trainer in trainers)
{
    Console.WriteLine();
    Console.WriteLine($"=== {trainer.Name} ===");

    var (model, metrics) = trainer.Train(mlContext, split.TrainSet, split.TestSet);

    Console.WriteLine(metrics);

    var modelPath = Path.Combine(modelsDir, trainer.ModelFileName);
    mlContext.Model.Save(model, data.Schema, modelPath);
    Console.WriteLine($"Saved model to {modelPath}");
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
