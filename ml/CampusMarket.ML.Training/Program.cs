using CampusMarket.ML.Training;
using Microsoft.ML;

const int Seed = 2026;

var mlContext = new MLContext(seed: Seed);

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

var firstSplit = mlContext.Data.TrainTestSplit(data, testFraction: 0.3, seed: Seed);
var secondSplit = mlContext.Data.TrainTestSplit(firstSplit.TestSet, testFraction: 0.5, seed: Seed);

var trainSet = firstSplit.TrainSet;
var validationSet = secondSplit.TrainSet;
var testSet = secondSplit.TestSet;

Console.WriteLine($"Train rows:      {Count(trainSet)}");
Console.WriteLine($"Validation rows: {Count(validationSet)}");
Console.WriteLine($"Test rows:       {Count(testSet)}");

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
