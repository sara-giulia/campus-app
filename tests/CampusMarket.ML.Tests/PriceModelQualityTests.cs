using CampusMarket.Api.Infrastructure.ML;
using CampusMarket.ML.Training;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CampusMarket.ML.Tests;

// Quality bar for the saved price-regression model.
//  - Loads ml/models/price-regression.zip and ml/data/listings.csv.
//  - Uses the non-spam rows of the same 70/15/15 split as training
//    (see DatasetSplit in ml/CampusMarket.ML.Training), scoring the 15% test part.
//  - Fails if R² on the test slice falls below MinRSquared.
//  - Fails if, for the same title/description/category, a "New" item is priced
//    lower than or equal to a "Worn" one: condition must carry weight.
public sealed class PriceModelQualityTests
{
    // Set for the price v2 winner at the time (FastTree on log1p(Price), test R²
    // 0.451). The current winner (LightGbm on log1p(Price) with the Text features,
    // picked by MAE on validation; see docs/ml/price.md) has test R² 0.480 on unseen
    // item types (grouped split by TemplateId); grouped 5-fold CV gives R² 0.449 ± 0.091.
    // The bar was not tightened: it still tolerates ~0.08 of drift without going green
    // on a materially worse model.
    private const double MinRSquared = 0.40;

    [Fact]
    public void Model_reaches_the_r2_bar_on_the_non_spam_test_split()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);

        var (model, _) = LoadModel(mlContext);
        var testSet = LoadNonSpamTestSplit(mlContext);

        var scored = model.Transform(testSet);
        var rows = mlContext.Data.CreateEnumerable<ScoredRow>(scored, reuseRowObject: false).ToList();

        var r2 = ComputeRSquared(
            rows.Select(r => (double)r.Price),
            rows.Select(r => (double)r.Score));

        r2.Should().BeGreaterThanOrEqualTo(MinRSquared,
            "the committed model must stay at least at the agreed quality bar on the hold-out split.");
    }

    [Theory]
    [InlineData("Laptop Lenovo ThinkPad T480, i5 + 16GB RAM", "Laptop folosit la cursuri, bateria tine 4 ore.", "Electronics")]
    [InlineData("Notițe Analiză matematică, an I semestrul 1", "Notițe făcute la curs, aproape toată materia acoperită.", "Notes")]
    [InlineData("Casio fx-991EX ClassWiz, pentru examen", "Calculator in stare perfecta, baterie plina.", "Electronics")]
    public void Prices_a_new_item_higher_than_a_worn_one_with_everything_else_equal(string title, string description, string category)
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var (model, _) = LoadModel(mlContext);

        var engine = mlContext.Model.CreatePredictionEngine<PriceInput, PricePrediction>(model);

        var newPrice = engine.Predict(InputFor(title, description, category, "New")).Score;
        var wornPrice = engine.Predict(InputFor(title, description, category, "Worn")).Score;

        newPrice.Should().BeGreaterThan(wornPrice,
            $"'{title}' should be priced higher when New than when Worn (New={newPrice:0}, Worn={wornPrice:0}).");
    }

    private static (ITransformer Model, DataViewSchema Schema) LoadModel(MLContext mlContext)
    {
        var modelPath = Path.Combine(TestPaths.MlDirectory, "models", "price-regression.zip");
        File.Exists(modelPath).Should().BeTrue($"the model file must exist at {modelPath}");
        var model = mlContext.Model.Load(modelPath, out var schema);
        return (model, schema);
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

        // PriceModelInput adds the four text features a model trained on the Text feature set needs.
        var nonSpam = mlContext.Data.CreateEnumerable<ListingRow>(test, reuseRowObject: false)
            .Where(r => !r.IsSpam)
            .Select(PriceModelInput.From)
            .ToList();
        return mlContext.Data.LoadFromEnumerable(nonSpam);
    }

    private static PriceInput InputFor(string title, string description, string category, string condition)
    {
        var features = PriceTextFeatures.Extract(title, description);
        return new PriceInput
        {
            Title = title,
            Description = description,
            Category = category,
            Condition = condition,
            Brand = features.Brand,
            StorageGb = features.StorageGb,
            CpuTier = features.CpuTier,
            IsNewOrSealed = features.IsNewOrSealed
        };
    }

    private static double ComputeRSquared(IEnumerable<double> actual, IEnumerable<double> predicted)
    {
        var actualList = actual.ToList();
        var predictedList = predicted.ToList();
        var mean = actualList.Average();

        double ssTotal = 0;
        double ssRes = 0;
        for (var i = 0; i < actualList.Count; i++)
        {
            ssTotal += (actualList[i] - mean) * (actualList[i] - mean);
            var err = actualList[i] - predictedList[i];
            ssRes += err * err;
        }

        return ssTotal > 0 ? 1 - ssRes / ssTotal : 0;
    }

    private sealed class PriceInput
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Condition { get; set; } = string.Empty;

        public string Brand { get; set; } = PriceTextFeatures.NoBrand;

        public float StorageGb { get; set; }

        public float CpuTier { get; set; }

        public float IsNewOrSealed { get; set; }
    }

    private sealed class PricePrediction
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }

    private sealed class ScoredRow
    {
        public float Price { get; set; }

        public float Score { get; set; }
    }
}
