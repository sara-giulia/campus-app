using CampusMarket.Api.Common.Moderation;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.ML;

// Spam when the probability reaches the configured threshold (Ml:Spam:Threshold, chosen on validation
// data), not the model's built-in 0.5.
public sealed class SpamDetector(PredictionEnginePool<SpamInput, SpamPrediction> pool, IOptions<SpamDetectionOptions> options)
    : ISpamDetector
{
    public const string ModelName = "SpamDetection";

    public SpamCheck Check(string title, string? description, decimal price)
    {
        var input = new SpamInput
        {
            Title = title,
            Description = description ?? string.Empty,
            Price = (float)price
        };

        var prediction = pool.Predict(ModelName, input);
        var probability = Math.Round(prediction.Probability, 2, MidpointRounding.AwayFromZero);

        return new SpamCheck(prediction.Probability >= options.Value.Threshold, probability);
    }
}
