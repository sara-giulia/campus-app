using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.GetConversation;

public class GetConversationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Member BuildSeller() =>
        Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;

    private static Member BuildBuyer() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Member BuildOtherMember() =>
        Member.Create("Dan Popa", "dan@campus.ro", Now).Value;

    private static Listing BuildActiveListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static Conversation BuildConversationWithThreeMessages(Guid buyerId, Guid sellerId, Listing listing)
    {
        var conversation = Conversation.Start(buyerId, listing, "Mai e disponibil?", Now).Value;
        conversation.Send(sellerId, "Da, este", Now.AddMinutes(5));
        conversation.Send(buyerId, "Il iau maine", Now.AddMinutes(10));
        return conversation;
    }

    private static GetConversationHandler BuildHandler(
        FakeConversationRepository conversations,
        FakeListingRepository listings,
        FakeMemberRepository members) =>
        new(conversations, listings, members, new FakeOfferRepository());

    [Fact]
    public async Task Given_Buyer_When_Handling_Then_ReturnsConversationWithMessagesInNumberOrder()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var conversation = BuildConversationWithThreeMessages(buyer.Id, seller.Id, listing);
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetConversationQuery(buyer.Id, conversation.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(conversation.Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.ListingTitle.Should().Be("Algebra book");
        result.Value.SellerId.Should().Be(seller.Id);
        result.Value.SellerName.Should().Be("Ion Ionescu");
        result.Value.BuyerId.Should().Be(buyer.Id);
        result.Value.BuyerName.Should().Be("Ana Pop");
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.Messages.Select(m => m.Number).Should().Equal(1, 2, 3);
        result.Value.Messages.Select(m => m.Id).Should().Equal(conversation.Messages.Select(m => m.Id));
        result.Value.Messages.Select(m => m.SenderId).Should().Equal(buyer.Id, seller.Id, buyer.Id);
        result.Value.Messages.Select(m => m.Text).Should().Equal("Mai e disponibil?", "Da, este", "Il iau maine");
        result.Value.Messages.Select(m => m.SentAt).Should().Equal(Now, Now.AddMinutes(5), Now.AddMinutes(10));
        conversations.Items.Should().ContainSingle();
        conversation.Messages.Should().HaveCount(3);
        listings.Items.Should().HaveCount(1);
        members.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_Seller_When_Handling_Then_ReturnsConversationWithMessagesInNumberOrder()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var conversation = BuildConversationWithThreeMessages(buyer.Id, seller.Id, listing);
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetConversationQuery(seller.Id, conversation.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(conversation.Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.ListingTitle.Should().Be("Algebra book");
        result.Value.SellerId.Should().Be(seller.Id);
        result.Value.SellerName.Should().Be("Ion Ionescu");
        result.Value.BuyerId.Should().Be(buyer.Id);
        result.Value.BuyerName.Should().Be("Ana Pop");
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.Messages.Select(m => m.Number).Should().Equal(1, 2, 3);
        result.Value.Messages.Select(m => m.Text).Should().Equal("Mai e disponibil?", "Da, este", "Il iau maine");
        conversations.Items.Should().ContainSingle();
        conversation.Messages.Should().HaveCount(3);
        listings.Items.Should().HaveCount(1);
        members.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_OtherMember_When_Handling_Then_ReturnsForbidden()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var other = BuildOtherMember();
        var listing = BuildActiveListing(seller.Id);
        var conversation = BuildConversationWithThreeMessages(buyer.Id, seller.Id, listing);
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        members.Items[other.Id] = other;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetConversationQuery(other.Id, conversation.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can read this conversation.");
        conversations.Items.Should().ContainSingle();
        conversation.Messages.Should().HaveCount(3);
        listings.Items.Should().HaveCount(1);
        members.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_UnknownConversation_When_Handling_Then_ReturnsNotFound()
    {
        var buyer = BuildBuyer();
        var conversationId = Guid.NewGuid();
        var conversations = new FakeConversationRepository();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetConversationQuery(buyer.Id, conversationId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Conversation.NotFound");
        result.Error.Message.Should().Be($"Conversation '{conversationId}' was not found.");
        conversations.Items.Should().BeEmpty();
        listings.Items.Should().BeEmpty();
        members.Items.Should().HaveCount(1);
    }
}
