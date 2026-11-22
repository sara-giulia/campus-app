using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Observability;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

public sealed class CategorySuggester(
    PredictionEnginePool<CategoryInput, CategoryPrediction> pool,
    MetricsRegistry metrics) : ICategorySuggester
{
    public const string ModelName = "CategoryClassification";
    private const string MetricModel = "category";

    public CategorySuggestion Suggest(string title, string? description)
    {
        var input = new CategoryInput
        {
            Title = title,
            Description = description ?? string.Empty
        };

        var prediction = metrics.MeasurePrediction(MetricModel, () => pool.Predict(ModelName, input));

        // The model was trained on the Category names, so the label always parses.
        var category = Enum.Parse<Category>(prediction.PredictedLabel);
        var confidence = Math.Round(prediction.Score.Max(), 2, MidpointRounding.AwayFromZero);

        return new CategorySuggestion(category, confidence);
    }
}
