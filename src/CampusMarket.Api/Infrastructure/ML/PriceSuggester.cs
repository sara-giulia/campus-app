using CampusMarket.Api.Common.Pricing;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Observability;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

// PredictionEnginePool is thread-safe; it is the recommended wrapper around
// PredictionEngine for ASP.NET Core and watches the model file for hot reloads.
public sealed class PriceSuggester(
    PredictionEnginePool<PriceInput, PricePrediction> pool,
    MetricsRegistry metrics) : IPriceSuggester
{
    public const string ModelName = "PriceRegression";
    private const string MetricModel = "price";

    public int Suggest(string title, string? description, Category category, Condition condition)
    {
        var features = PriceTextFeatures.Extract(title, description);
        var input = new PriceInput
        {
            Title = title,
            Description = description ?? string.Empty,
            Category = category.ToString(),
            Condition = condition.ToString(),
            Brand = features.Brand,
            StorageGb = features.StorageGb,
            CpuTier = features.CpuTier,
            IsNewOrSealed = features.IsNewOrSealed
        };

        var prediction = metrics.MeasurePrediction(MetricModel, () => pool.Predict(ModelName, input));
        var rounded = (int)Math.Round(prediction.Score, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }
}
