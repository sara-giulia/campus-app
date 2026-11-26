using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations;

public class MessageSpamGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private const string SpamMessage =
        "The message looks like a scam (money or card data asked for, or a suspicious link) and was not sent.";

    private static Message BuildMessage(string text)
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return Conversation.Start(BuyerId, listing, text, Now).Value.Messages[0];
    }

    [Fact]
    public void Given_CleanVerdict_When_Checking_Then_RecordsProbabilityAndNotSuspicious()
    {
        var message = BuildMessage("Mai e disponibil?");
        var detector = new FakeMessageSpamDetector { Result = new MessageSpamCheck(MessageSpamVerdict.Clean, 0.05) };

        var result = MessageSpamGuard.Check(detector, message);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(message);
        message.SpamProbability.Should().Be(0.05);
        message.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void Given_SuspiciousVerdict_When_Checking_Then_RecordsProbabilityAndSuspicious()
    {
        var message = BuildMessage("Intra pe www.oferta.ro");
        var detector = new FakeMessageSpamDetector { Result = new MessageSpamCheck(MessageSpamVerdict.Suspicious, 0.7) };

        var result = MessageSpamGuard.Check(detector, message);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(message);
        message.SpamProbability.Should().Be(0.7);
        message.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public void Given_BlockedVerdict_When_Checking_Then_ReturnsSpamErrorAndLeavesMessageUnchanged()
    {
        var message = BuildMessage("Trimite 150 lei avans");
        var detector = new FakeMessageSpamDetector { Result = new MessageSpamCheck(MessageSpamVerdict.Blocked, 0.99) };

        var result = MessageSpamGuard.Check(detector, message);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Spam");
        result.Error.Message.Should().Be(SpamMessage);
        message.SpamProbability.Should().BeNull();
        message.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void Given_Message_When_Checking_Then_DetectorGetsTheSavedTextOnce()
    {
        var message = BuildMessage("  Mai e disponibil?  ");
        var detector = new FakeMessageSpamDetector();

        MessageSpamGuard.Check(detector, message);

        detector.CallCount.Should().Be(1);
        detector.LastText.Should().Be("Mai e disponibil?");
    }

    [Fact]
    public void Given_Guard_When_BuildingBlockedError_Then_ReturnsSpamValidationError()
    {
        var error = MessageSpamGuard.BlockedError();

        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Message.Spam");
        error.Message.Should().Be(SpamMessage);
    }
}
