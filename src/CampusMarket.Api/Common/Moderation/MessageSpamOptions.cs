namespace CampusMarket.Api.Common.Moderation;

// From Threshold a message is shown with a warning, from BlockThreshold it is not sent. Both are chosen by
// cross-validation when the model is trained ("settings" in ml/models/message-spam.json) and configured
// in appsettings.json as Ml:MessageSpam.
public sealed class MessageSpamOptions
{
    public const string SectionName = "Ml:MessageSpam";

    public double Threshold { get; set; } = 0.6;

    public double BlockThreshold { get; set; } = 0.95;
}
