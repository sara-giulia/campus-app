using CampusMarket.Api.Common.Moderation;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeMessageSpamDetector : IMessageSpamDetector
{
    public MessageSpamCheck Result { get; set; } = new(MessageSpamVerdict.Clean, 0.05);

    public int CallCount { get; private set; }

    public string? LastText { get; private set; }

    public MessageSpamCheck Check(string text)
    {
        CallCount++;
        LastText = text;
        return Result;
    }
}
