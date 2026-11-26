namespace CampusMarket.Api.Infrastructure.ML;

// The column the message spam model reads. The name matches MessageRow in the training project.
public sealed class MessageSpamInput
{
    public string Text { get; set; } = string.Empty;
}
