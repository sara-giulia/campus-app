namespace CampusMarket.Api.Common.Moderation;

// Tells whether a chat message looks like spam or a scam. The implementation uses the message spam
// model and MessageSpamRules (Infrastructure/ML/MessageSpamDetector); handlers only see this interface.
public interface IMessageSpamDetector
{
    MessageSpamCheck Check(string text);
}

// Clean: sent as it is. Suspicious: sent, but shown with a warning. Blocked: not sent at all.
public enum MessageSpamVerdict
{
    Clean,
    Suspicious,
    Blocked
}

// Probability is the model's, between 0 and 1, with two decimals.
public sealed record MessageSpamCheck(MessageSpamVerdict Verdict, double Probability);
