using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Conversations;

public class MessageSpamTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");

    private static Message BuildMessage()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now).Value.Messages[0];
    }

    [Fact]
    public void Given_NewConversation_When_ReadingFirstMessage_Then_IsNotCheckedAndNotSuspicious()
    {
        var message = BuildMessage();

        var probability = message.SpamProbability;

        probability.Should().BeNull();
        message.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void Given_NewMessage_When_RecordingSpamCheck_Then_SetsProbabilityAndSuspicious()
    {
        var message = BuildMessage();

        message.RecordSpamCheck(0.42, true);

        message.SpamProbability.Should().Be(0.42);
        message.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public void Given_CheckedMessage_When_RecordingSpamCheckAgain_Then_ReplacesBothValues()
    {
        var message = BuildMessage();
        message.RecordSpamCheck(0.42, true);

        message.RecordSpamCheck(0.1, false);

        message.SpamProbability.Should().Be(0.1);
        message.IsSuspicious.Should().BeFalse();
    }
}
