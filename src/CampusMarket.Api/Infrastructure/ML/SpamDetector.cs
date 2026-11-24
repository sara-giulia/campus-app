using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Infrastructure.Observability;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.ML;

// Spam when the probability reaches the configured threshold (Ml:Spam:Threshold, chosen on validation
// data), not the model's built-in 0.5, or when one of the SpamRules matches.
public sealed class SpamDetector(PredictionEnginePool<SpamInput, SpamPrediction> pool, IOptions<SpamDetectionOptions> options)
// data), not the model's built-in 0.5.
public sealed class SpamDetector(
    PredictionEnginePool<SpamInput, SpamPrediction> pool,
    IOptions<SpamDetectionOptions> options,
    MetricsRegistry metrics)
    : ISpamDetector
{
    public const string ModelName = "SpamDetection";
    private const string MetricModel = "spam";

    public SpamCheck Check(string title, string? description, decimal price)
    {
        var input = new SpamInput
        {
            Title = title,
            Description = description ?? string.Empty,
            Price = (float)price
        };

        var prediction = pool.Predict(ModelName, input);
        var byModel = prediction.Probability >= options.Value.Threshold;
        var prediction = metrics.MeasurePrediction(MetricModel, () => pool.Predict(ModelName, input));
        var probability = Math.Round(prediction.Probability, 2, MidpointRounding.AwayFromZero);

        // A clear rule (link, phone, contact outside the app, money bait) makes it spam even when the
        // model missed it; the probability shown to the admin is then at least SpamRules.RuleProbability.
        var probability = !byModel && SpamRules.Matches(title, description)
            ? Math.Max(prediction.Probability, SpamRules.RuleProbability)
            : prediction.Probability;

        return new SpamCheck(probability >= options.Value.Threshold, Math.Round(probability, 2, MidpointRounding.AwayFromZero));
    }
}
