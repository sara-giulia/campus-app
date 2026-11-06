using CampusMarket.Api.Common.Moderation;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

public sealed class SpamDetector(PredictionEnginePool<SpamInput, SpamPrediction> pool) : ISpamDetector
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

        return new SpamCheck(prediction.IsSpam, probability);
    }
}
