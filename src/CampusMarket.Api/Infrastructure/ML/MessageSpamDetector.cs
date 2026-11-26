using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Infrastructure.Observability;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.ML;

// Blocked only when the model is very sure (Ml:MessageSpam:BlockThreshold, at which no normal message
// was blocked in cross-validation). Suspicious when the model reaches Ml:MessageSpam:Threshold or one of
// MessageSpamRules matches. The model gives SpamPrediction, like the spam model of the listings.
public sealed class MessageSpamDetector(
    PredictionEnginePool<MessageSpamInput, SpamPrediction> pool,
    IOptions<MessageSpamOptions> options,
    MetricsRegistry metrics)
    : IMessageSpamDetector
{
    public const string ModelName = "MessageSpam";
    private const string MetricModel = "message-spam";

    public MessageSpamCheck Check(string text)
    {
        var prediction = metrics.MeasurePrediction(MetricModel, () => pool.Predict(ModelName, new MessageSpamInput { Text = text }));
        var probability = prediction.Probability;

        var verdict = probability >= options.Value.BlockThreshold
            ? MessageSpamVerdict.Blocked
            : probability >= options.Value.Threshold || MessageSpamRules.Matches(text)
                ? MessageSpamVerdict.Suspicious
                : MessageSpamVerdict.Clean;

        return new MessageSpamCheck(verdict, Math.Round(probability, 2, MidpointRounding.AwayFromZero));
    }
}
