using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Conversations;

public class ConversationReadTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildActiveListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static Conversation BuildConversation() =>
        Conversation.Start(BuyerId, BuildActiveListing(), "Mai e disponibil?", Now).Value;

    private static Conversation BuildConversationWithThreeBuyerMessages()
    {
        var conversation = BuildConversation();
        conversation.Send(BuyerId, "Il iau azi", Now.AddMinutes(1));
        conversation.Send(BuyerId, "Raspunde te rog", Now.AddMinutes(2));
        return conversation;
    }

    [Fact]
    public void Given_NewConversation_When_Started_Then_BuyerMarkIsOneAndSellerMarkIsZero()
    {
        var listing = BuildActiveListing();

        var conversation = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now).Value;

        conversation.BuyerLastReadNumber.Should().Be(1);
        conversation.SellerLastReadNumber.Should().Be(0);
    }

    [Fact]
    public void Given_NewConversation_When_CountingUnread_Then_SellerHasOneAndBuyerHasNone()
    {
        var conversation = BuildConversation();

        var sellerUnread = conversation.UnreadCountFor(SellerId);
        var buyerUnread = conversation.UnreadCountFor(BuyerId);

        sellerUnread.Should().Be(1);
        buyerUnread.Should().Be(0);
    }

    [Fact]
    public void Given_NewConversation_When_SellerAnswers_Then_SellerMarkIsTwoAndOnlyBuyerHasUnread()
    {
        var conversation = BuildConversation();

        conversation.Send(SellerId, "Da, este", Now.AddMinutes(5));

        conversation.SellerLastReadNumber.Should().Be(2);
        conversation.BuyerLastReadNumber.Should().Be(1);
        conversation.UnreadCountFor(SellerId).Should().Be(0);
        conversation.UnreadCountFor(BuyerId).Should().Be(1);
    }

    [Fact]
    public void Given_ThreeBuyerMessagesInARow_When_CountingUnreadForSeller_Then_ReturnsThree()
    {
        var conversation = BuildConversationWithThreeBuyerMessages();

        var unread = conversation.UnreadCountFor(SellerId);

        unread.Should().Be(3);
        conversation.UnreadCountFor(BuyerId).Should().Be(0);
    }

    [Fact]
    public void Given_ThreeUnreadForSeller_When_SellerMarksRead_Then_ReturnsThreeAndMovesOnlySellerMark()
    {
        var conversation = BuildConversationWithThreeBuyerMessages();

        var result = conversation.MarkReadBy(SellerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
        conversation.SellerLastReadNumber.Should().Be(conversation.Messages.Max(m => m.Number));
        conversation.SellerLastReadNumber.Should().Be(3);
        conversation.BuyerLastReadNumber.Should().Be(3);
        conversation.UnreadCountFor(SellerId).Should().Be(0);
    }

    [Fact]
    public void Given_SellerAnswered_When_BuyerMarksRead_Then_ReturnsOneAndMovesOnlyBuyerMark()
    {
        var conversation = BuildConversation();
        conversation.Send(SellerId, "Da, este", Now.AddMinutes(5));
        conversation.Send(SellerId, "Poti veni azi", Now.AddMinutes(6));

        var result = conversation.MarkReadBy(BuyerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        conversation.BuyerLastReadNumber.Should().Be(3);
        conversation.SellerLastReadNumber.Should().Be(3);
        conversation.UnreadCountFor(BuyerId).Should().Be(0);
    }

    [Fact]
    public void Given_SellerAlreadyMarkedRead_When_MarkingReadAgain_Then_ReturnsZeroAndChangesNothing()
    {
        var conversation = BuildConversationWithThreeBuyerMessages();
        conversation.MarkReadBy(SellerId);

        var result = conversation.MarkReadBy(SellerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        conversation.SellerLastReadNumber.Should().Be(3);
        conversation.BuyerLastReadNumber.Should().Be(3);
    }

    [Fact]
    public void Given_NothingUnreadForBuyer_When_BuyerMarksRead_Then_ReturnsZeroAndKeepsMark()
    {
        var conversation = BuildConversation();

        var result = conversation.MarkReadBy(BuyerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(1);
        conversation.SellerLastReadNumber.Should().Be(0);
    }

    [Fact]
    public void Given_Stranger_When_MarkingRead_Then_ReturnsForbiddenAndKeepsBothMarks()
    {
        var conversation = BuildConversationWithThreeBuyerMessages();

        var result = conversation.MarkReadBy(OtherMemberId);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can read this conversation.");
        conversation.BuyerLastReadNumber.Should().Be(3);
        conversation.SellerLastReadNumber.Should().Be(0);
        conversation.UnreadCountFor(SellerId).Should().Be(3);
    }

    [Fact]
    public void Given_Conversation_When_SellerSendsEmptyText_Then_MarksAreUnchanged()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(SellerId, "   ", Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Message.Empty");
        conversation.SellerLastReadNumber.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(1);
        conversation.UnreadCountFor(SellerId).Should().Be(1);
    }

    [Fact]
    public void Given_Conversation_When_SellerSendsTooLongText_Then_MarksAreUnchanged()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(SellerId, new string('a', Message.MaxTextLength + 1), Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Message.TooLong");
        conversation.SellerLastReadNumber.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(1);
        conversation.UnreadCountFor(SellerId).Should().Be(1);
    }

    [Fact]
    public void Given_Conversation_When_StrangerSends_Then_MarksAreUnchanged()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(OtherMemberId, "Salut", Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        conversation.SellerLastReadNumber.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(1);
        conversation.UnreadCountFor(SellerId).Should().Be(1);
    }

    [Fact]
    public void Given_SellerMarkedRead_When_BuyerSendsNewMessage_Then_SellerHasOneUnreadAgain()
    {
        var conversation = BuildConversationWithThreeBuyerMessages();
        conversation.MarkReadBy(SellerId);

        conversation.Send(BuyerId, "Mai esti acolo?", Now.AddMinutes(10));

        conversation.UnreadCountFor(SellerId).Should().Be(1);
        conversation.SellerLastReadNumber.Should().Be(3);
        conversation.BuyerLastReadNumber.Should().Be(4);
    }
}
