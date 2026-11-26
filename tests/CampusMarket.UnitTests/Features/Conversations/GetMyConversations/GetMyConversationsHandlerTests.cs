using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.GetMyConversations;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.GetMyConversations;

public class GetMyConversationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Member BuildSeller() =>
        Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;

    private static Member BuildBuyer() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Member BuildOtherBuyer() =>
        Member.Create("Dan Popa", "dan@campus.ro", Now).Value;

    private static Member BuildOtherMember() =>
        Member.Create("Maria Stan", "maria@campus.ro", Now).Value;

    private static Listing BuildActiveListing(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static GetMyConversationsHandler BuildHandler(
        FakeConversationRepository conversations,
        FakeListingRepository listings,
        FakeMemberRepository members) =>
        new(conversations, listings, members);

    [Fact]
    public async Task Given_MemberWithNoConversation_When_Handling_Then_ReturnsEmptyList()
    {
        var member = BuildBuyer();
        var conversations = new FakeConversationRepository();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetMyConversationsQuery(member.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        conversations.Items.Should().BeEmpty();
        members.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_SellerWithTwoConversations_When_Handling_Then_ReturnsBothLatestFirstWithBuyersAsOtherMember()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var otherBuyer = BuildOtherBuyer();
        var algebra = BuildActiveListing(seller.Id, "Algebra book");
        var physics = BuildActiveListing(seller.Id, "Physics book");
        var older = Conversation.Start(buyer.Id, algebra, "Mai e disponibil?", Now).Value;
        older.Send(seller.Id, "Da, este", Now.AddMinutes(5));
        var newer = Conversation.Start(otherBuyer.Id, physics, "Cat costa?", Now.AddMinutes(10)).Value;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(older);
        conversations.Items.Add(newer);
        var listings = new FakeListingRepository();
        listings.Items[algebra.Id] = algebra;
        listings.Items[physics.Id] = physics;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        members.Items[otherBuyer.Id] = otherBuyer;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetMyConversationsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Id.Should().Be(newer.Id);
        result.Value.Items[0].ListingId.Should().Be(physics.Id);
        result.Value.Items[0].ListingTitle.Should().Be("Physics book");
        result.Value.Items[0].OtherMemberId.Should().Be(otherBuyer.Id);
        result.Value.Items[0].OtherMemberName.Should().Be("Dan Popa");
        result.Value.Items[0].LastMessage.Should().Be("Cat costa?");
        result.Value.Items[0].LastMessageSenderId.Should().Be(otherBuyer.Id);
        result.Value.Items[0].LastMessageAt.Should().Be(Now.AddMinutes(10));
        result.Value.Items[0].MessageCount.Should().Be(1);
        result.Value.Items[1].Id.Should().Be(older.Id);
        result.Value.Items[1].ListingId.Should().Be(algebra.Id);
        result.Value.Items[1].ListingTitle.Should().Be("Algebra book");
        result.Value.Items[1].OtherMemberId.Should().Be(buyer.Id);
        result.Value.Items[1].OtherMemberName.Should().Be("Ana Pop");
        result.Value.Items[1].LastMessage.Should().Be("Da, este");
        result.Value.Items[1].LastMessageSenderId.Should().Be(seller.Id);
        result.Value.Items[1].LastMessageAt.Should().Be(Now.AddMinutes(5));
        result.Value.Items[1].MessageCount.Should().Be(2);
        conversations.Items.Should().HaveCount(2);
        older.Messages.Should().HaveCount(2);
        newer.Messages.Should().ContainSingle();
        listings.Items.Should().HaveCount(2);
        members.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_Buyer_When_Handling_Then_OtherMemberIsSeller()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id, "Algebra book");
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", Now).Value;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(conversation);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetMyConversationsQuery(buyer.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(conversation.Id);
        result.Value.Items[0].OtherMemberId.Should().Be(seller.Id);
        result.Value.Items[0].OtherMemberName.Should().Be("Ion Ionescu");
        result.Value.Items[0].LastMessage.Should().Be("Mai e disponibil?");
        result.Value.Items[0].LastMessageSenderId.Should().Be(buyer.Id);
        result.Value.Items[0].MessageCount.Should().Be(1);
        conversations.Items.Should().ContainSingle();
        listings.Items.Should().HaveCount(1);
        members.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ConversationsOfOtherMembers_When_Handling_Then_ReturnsOnlyTheMembersConversations()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var otherBuyer = BuildOtherBuyer();
        var otherMember = BuildOtherMember();
        var listing = BuildActiveListing(seller.Id, "Algebra book");
        var otherListing = BuildActiveListing(otherMember.Id, "Physics book");
        var mine = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", Now).Value;
        var notMine = Conversation.Start(otherBuyer.Id, otherListing, "Cat costa?", Now.AddMinutes(10)).Value;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(mine);
        conversations.Items.Add(notMine);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        listings.Items[otherListing.Id] = otherListing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        members.Items[otherBuyer.Id] = otherBuyer;
        members.Items[otherMember.Id] = otherMember;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetMyConversationsQuery(buyer.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(1);
        result.Value.Items.Select(i => i.Id).Should().Equal(mine.Id);
        conversations.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
        members.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task Given_TwoConversationsWithSameLastMessageAt_When_Handling_Then_OrdersById()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var otherBuyer = BuildOtherBuyer();
        var algebra = BuildActiveListing(seller.Id, "Algebra book");
        var physics = BuildActiveListing(seller.Id, "Physics book");
        var first = Conversation.Start(buyer.Id, algebra, "Mai e disponibil?", Now).Value;
        var second = Conversation.Start(otherBuyer.Id, physics, "Cat costa?", Now).Value;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(first);
        conversations.Items.Add(second);
        var listings = new FakeListingRepository();
        listings.Items[algebra.Id] = algebra;
        listings.Items[physics.Id] = physics;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        members.Items[otherBuyer.Id] = otherBuyer;
        var handler = BuildHandler(conversations, listings, members);
        var query = new GetMyConversationsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().BeInAscendingOrder(i => i.Id);
        conversations.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
        members.Items.Should().HaveCount(3);
    }
}
