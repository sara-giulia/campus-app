namespace CampusMarket.Api.Common.Moderation;

// Tells whether a listing looks like spam. The implementation uses the ML model
// (Infrastructure/ML/SpamDetector); handlers only see this interface.
public interface ISpamDetector
{
    SpamCheck Check(string title, string? description, decimal price);
}

// Probability is between 0 and 1, with two decimals.
public sealed record SpamCheck(bool IsSpam, double Probability);
