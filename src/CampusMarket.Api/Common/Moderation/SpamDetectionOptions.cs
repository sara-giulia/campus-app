namespace CampusMarket.Api.Common.Moderation;

// A listing is spam when the model's probability reaches Threshold. The value is chosen on the
// validation data when the model is trained (SpamTrainer, "settings" in ml/models/spam-detection.json)
// and configured in appsettings.json as Ml:Spam:Threshold.
public sealed class SpamDetectionOptions
{
    public const string SectionName = "Ml:Spam";

    public double Threshold { get; set; } = 0.4;
}
