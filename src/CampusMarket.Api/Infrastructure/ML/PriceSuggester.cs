using CampusMarket.Api.Common.Pricing;
using CampusMarket.Api.Domain.Listings;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

// PredictionEnginePool is thread-safe; it is the recommended wrapper around
// PredictionEngine for ASP.NET Core and watches the model file for hot reloads.
public sealed class PriceSuggester(PredictionEnginePool<PriceInput, PricePrediction> pool) : IPriceSuggester
{
    public const string ModelName = "PriceRegression";

    public int Suggest(string title, string? description, Category category, Condition condition)
    {
        var input = new PriceInput
        {
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            Category = category.ToString(),
            Condition = condition.ToString()
        };

        var prediction = pool.Predict(ModelName, input);
        var rounded = (int)Math.Round(prediction.Score, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }
}
