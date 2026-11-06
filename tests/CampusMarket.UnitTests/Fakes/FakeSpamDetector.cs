using CampusMarket.Api.Common.Moderation;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeSpamDetector : ISpamDetector
{
    public SpamCheck Result { get; set; } = new(false, 0.1);

    public int CallCount { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastDescription { get; private set; }

    public decimal? LastPrice { get; private set; }

    public SpamCheck Check(string title, string? description, decimal price)
    {
        CallCount++;
        LastTitle = title;
        LastDescription = description;
        LastPrice = price;
        return Result;
    }
}
