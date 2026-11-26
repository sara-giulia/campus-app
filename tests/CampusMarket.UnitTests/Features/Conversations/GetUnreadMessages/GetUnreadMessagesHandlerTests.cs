using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Conversations.GetUnreadMessages;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.GetUnreadMessages;

public class GetUnreadMessagesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherBuyerId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ThirdBuyerId = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherSellerId = new("55555555-5555-5555-5555-555555555555");

    private static Listing BuildActiveListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static Conversation Start(Guid buyerId, Guid sellerId) =>
        Conversation.Start(buyerId, BuildActiveListing(sellerId), "Mai e disponibil?", Now).Value;

    [Fact]
    public async Task Given_NoConversations_When_Handling_Then_ReturnsZeroAndZero()
    {
        var conversations = new FakeConversationRepository();
        var handler = new GetUnreadMessagesHandler(conversations);

        var result = await handler.HandleAsync(new GetUnreadMessagesQuery(SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadMessages.Should().Be(0);
        result.Value.UnreadConversations.Should().Be(0);
        conversations.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwoUnreadAndOneReadConversation_When_Handling_Then_ReturnsThreeMessagesInTwoConversations()
    {
        var twoUnread = Start(BuyerId, SellerId);
        twoUnread.Send(BuyerId, "Il iau azi", Now.AddMinutes(1));
        var oneUnread = Start(OtherBuyerId, SellerId);
        var read = Start(ThirdBuyerId, SellerId);
        read.Send(SellerId, "Da, este", Now.AddMinutes(5));
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(twoUnread);
        conversations.Items.Add(oneUnread);
        conversations.Items.Add(read);
        var handler = new GetUnreadMessagesHandler(conversations);

        var result = await handler.HandleAsync(new GetUnreadMessagesQuery(SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadMessages.Should().Be(3);
        result.Value.UnreadConversations.Should().Be(2);
        conversations.Items.Should().HaveCount(3);
        twoUnread.SellerLastReadNumber.Should().Be(0);
        oneUnread.SellerLastReadNumber.Should().Be(0);
        read.SellerLastReadNumber.Should().Be(2);
    }

    [Fact]
    public async Task Given_ConversationOfTwoOtherMembers_When_Handling_Then_ItDoesNotCount()
    {
        var mine = Start(BuyerId, SellerId);
        var notMine = Start(OtherBuyerId, OtherSellerId);
        notMine.Send(OtherBuyerId, "Il iau azi", Now.AddMinutes(1));
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(mine);
        conversations.Items.Add(notMine);
        var handler = new GetUnreadMessagesHandler(conversations);

        var result = await handler.HandleAsync(new GetUnreadMessagesQuery(SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadMessages.Should().Be(1);
        result.Value.UnreadConversations.Should().Be(1);
        conversations.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_OnlyOwnMessagesAsBuyer_When_Handling_Then_ReturnsZeroAndZero()
    {
        var conversation = Start(BuyerId, SellerId);
        conversation.Send(BuyerId, "Il iau azi", Now.AddMinutes(1));
        conversation.Send(BuyerId, "Raspunde te rog", Now.AddMinutes(2));
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var handler = new GetUnreadMessagesHandler(conversations);

        var result = await handler.HandleAsync(new GetUnreadMessagesQuery(BuyerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadMessages.Should().Be(0);
        result.Value.UnreadConversations.Should().Be(0);
        conversation.BuyerLastReadNumber.Should().Be(3);
    }

    [Fact]
    public async Task Given_OnlyOwnMessagesAsSellerAfterFirstMessage_When_Handling_Then_ReturnsZeroAndZero()
    {
        var conversation = Start(BuyerId, SellerId);
        conversation.Send(SellerId, "Da, este", Now.AddMinutes(1));
        conversation.Send(SellerId, "Poti veni azi", Now.AddMinutes(2));
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var handler = new GetUnreadMessagesHandler(conversations);

        var result = await handler.HandleAsync(new GetUnreadMessagesQuery(SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadMessages.Should().Be(0);
        result.Value.UnreadConversations.Should().Be(0);
        conversation.SellerLastReadNumber.Should().Be(3);
    }
}
