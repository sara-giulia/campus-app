using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Features.Conversations;

// The spam check of a message that was just written, shared by StartConversation and SendMessage.
// A blocked message gives an error and the handler saves nothing; any other message keeps what the
// model said, and a suspicious one is shown with a warning.
public static class MessageSpamGuard
{
    public static Error BlockedError() => Error.Validation(
        "Message.Spam",
        "The message looks like a scam (money or card data asked for, or a suspicious link) and was not sent.");

    public static Result<Message> Check(IMessageSpamDetector detector, Message message)
    {
        var check = detector.Check(message.Text);
        if (check.Verdict == MessageSpamVerdict.Blocked)
            return Result<Message>.Failure(BlockedError());

        message.RecordSpamCheck(check.Probability, check.Verdict == MessageSpamVerdict.Suspicious);
        return message;
    }
}
