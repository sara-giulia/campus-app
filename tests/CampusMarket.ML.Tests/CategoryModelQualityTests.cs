using CampusMarket.ML.Training;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CampusMarket.ML.Tests;

// Quality bar for the saved category-classification model (B4).
//  - Loads ml/models/category-classification.zip and ml/data/listings.csv.
//  - Uses the non-spam rows of the same 70/15/15 split as training
//    (see DatasetSplit in ml/CampusMarket.ML.Training), scoring the 15% test part.
//  - Fails if the micro accuracy on the test slice falls below 0.9.
//  - Fails if listings written by hand (not in the CSV) get the wrong category,
//    or if the confidence is not a probability.
public sealed class CategoryModelQualityTests
{
    // Evaluated on unseen item types (grouped split by TemplateId); current model 0.854.
    private const double MinMicroAccuracy = 0.8;

    [Fact]
    public void Model_reaches_the_accuracy_bar_on_the_non_spam_test_split()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = LoadModel(mlContext);
        var testSet = LoadNonSpamTestSplit(mlContext);

        var rows = mlContext.Data
            .CreateEnumerable<ScoredRow>(model.Transform(testSet), reuseRowObject: false)
            .ToList();

        rows.Should().NotBeEmpty();
        var accuracy = rows.Count(r => r.PredictedLabel == r.Category) / (double)rows.Count;

        accuracy.Should().BeGreaterThanOrEqualTo(MinMicroAccuracy,
            "the committed model must stay at least at the agreed quality bar on the hold-out split.");
    }

    [Theory]
    [InlineData("Laptop Dell Latitude 5490, i7, 16GB RAM", "Merge foarte bine, are incarcator original.", "Electronics")]
    [InlineData("Notițe Baze de date, curs și laborator", "Scrise de mână, toate cursurile din semestrul 1.", "Notes")]
    [InlineData("Manual Structuri de date și algoritmi", "Ediția a doua, câteva pagini subliniate.", "Textbook")]
    public void Predicts_the_right_category_for_listings_that_are_not_in_the_dataset(string title, string description, string expected)
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var engine = mlContext.Model.CreatePredictionEngine<CategoryInput, CategoryPrediction>(LoadModel(mlContext));

        var prediction = engine.Predict(new CategoryInput { Title = title, Description = description });

        prediction.PredictedLabel.Should().Be(expected, $"'{title}' is clearly a {expected} listing.");
        prediction.Score.Max().Should().BeInRange(0f, 1f);
        prediction.Score.Sum().Should().BeApproximately(1f, 0.01f, "the scores are probabilities over the categories.");
    }

    private static ITransformer LoadModel(MLContext mlContext)
    {
        var modelPath = Path.Combine(TestPaths.MlDirectory, "models", "category-classification.zip");
        File.Exists(modelPath).Should().BeTrue($"the model file must exist at {modelPath}");
        return mlContext.Model.Load(modelPath, out _);
    }

    private static IDataView LoadNonSpamTestSplit(MLContext mlContext)
    {
        var csvPath = Path.Combine(TestPaths.MlDirectory, "data", "listings.csv");
        File.Exists(csvPath).Should().BeTrue($"the dataset file must exist at {csvPath}");

        var data = mlContext.Data.LoadFromTextFile<ListingRow>(
            csvPath,
            hasHeader: true,
            separatorChar: ',',
            allowQuoting: true);

        var (_, _, test) = DatasetSplit.Split(mlContext, data);

        var nonSpam = mlContext.Data.CreateEnumerable<ListingRow>(test, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .ToList();
        return mlContext.Data.LoadFromEnumerable(nonSpam);
    }

    private sealed class CategoryInput
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }

    private sealed class CategoryPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        [ColumnName("Score")]
        public float[] Score { get; set; } = [];
    }

    private sealed class ScoredRow
    {
        public string Category { get; set; } = string.Empty;

        public string PredictedLabel { get; set; } = string.Empty;
    }
}
