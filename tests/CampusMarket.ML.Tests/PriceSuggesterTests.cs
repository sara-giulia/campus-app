extern alias api;

using CampusMarket.ML.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using Microsoft.ML;
using api::CampusMarket.Api.Domain.Listings;
using api::CampusMarket.Api.Infrastructure.ML;
using api::CampusMarket.Api.Infrastructure.Observability;

namespace CampusMarket.ML.Tests;

// PriceSuggester must hand the four text features to the model. The model is a one-step pipeline
// built in memory, so its score is a number copied from the single column under test; no .zip file.
public sealed class PriceSuggesterTests
{
    private static readonly KeyValuePair<string, float>[] BrandScores =
    [
        new("none", 11f),
        new("lenovo", 22f),
        new("dell", 33f),
    ];

    [Fact]
    public void The_four_columns_reach_the_model_as_extracted_from_the_title()
    {
        const string title = "Laptop Lenovo ThinkPad i7 512GB sigilat";

        Suggest(NumberModel("StorageGb"), title).Should().Be(51210);
        Suggest(NumberModel("CpuTier"), title).Should().Be(710);
        Suggest(NumberModel("IsNewOrSealed"), title).Should().Be(110);
        Suggest(BrandModel(), title).Should().Be(22);
    }

    [Fact]
    public void A_title_without_features_sends_none_and_three_zeros()
    {
        const string title = "Scaun birou";

        Suggest(NumberModel("StorageGb"), title).Should().Be(10);
        Suggest(NumberModel("CpuTier"), title).Should().Be(10);
        Suggest(NumberModel("IsNewOrSealed"), title).Should().Be(10);
        Suggest(BrandModel(), title).Should().Be(11);
    }

    [Fact]
    public void The_features_come_from_the_title_and_from_the_description()
    {
        const string title = "Laptop Dell";
        const string description = "Disc de 1TB, procesor i5";

        Suggest(BrandModel(), title, description).Should().Be(33);
        Suggest(NumberModel("StorageGb"), title, description).Should().Be(102410);
        Suggest(NumberModel("CpuTier"), title, description).Should().Be(510);
    }

    [Fact]
    public void A_null_description_does_not_throw_and_the_title_fills_the_columns()
    {
        var act = () => Suggest(NumberModel("StorageGb"), "Laptop 256GB", null);

        act.Should().NotThrow();
        act().Should().Be(25610);
    }

    [Fact]
    public void The_score_is_rounded_away_from_zero()
    {
        Suggest(ConstantModel(100.5f), "Scaun birou").Should().Be(101);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(0.4f)]
    public void The_suggested_price_is_never_below_one(float score)
    {
        Suggest(ConstantModel(score), "Scaun birou").Should().Be(1);
    }

    private static int Suggest(ITransformer model, string title, string? description = null)
    {
        var services = new ServiceCollection();
        services.AddPredictionEnginePool<PriceInput, PricePrediction>();
        services.Configure<PredictionEnginePoolOptions<PriceInput, PricePrediction>>(
            PriceSuggester.ModelName, o => o.ModelLoader = new InMemoryModelLoader(model));
        using var provider = services.BuildServiceProvider();
        using var meters = new CampusMarketMeters();
        var suggester = new PriceSuggester(
            provider.GetRequiredService<PredictionEnginePool<PriceInput, PricePrediction>>(),
            new MetricsRegistry(meters, TimeProvider.System));

        return suggester.Suggest(title, description, Category.Electronics, Condition.Good);
    }

    // Score = column * 100 + 10, so that a 0 and a 1 stay apart and the clamp to 1 never hides a column.
    private static ITransformer NumberModel(string column) =>
        Fit(new MLContext(seed: 1).Transforms.Expression("Score", "(x) => x * 100 + 10", column));

    private static ITransformer BrandModel() =>
        Fit(new MLContext(seed: 1).Transforms.Conversion.MapValue("Score", BrandScores, "Brand"));

    private static ITransformer ConstantModel(float score) =>
        Fit(new MLContext(seed: 1).Transforms.Expression(
            "Score", FormattableString.Invariant($"(x) => x * 0 + {score}"), "StorageGb"));

    private static ITransformer Fit(IEstimator<ITransformer> estimator)
    {
        var mlContext = new MLContext(seed: 1);
        var empty = mlContext.Data.LoadFromEnumerable(Array.Empty<PriceInput>());
        return estimator.Fit(empty);
    }
}
