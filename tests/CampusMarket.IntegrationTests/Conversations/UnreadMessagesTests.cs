using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.Api.Features.Conversations.GetMyConversations;
using CampusMarket.Api.Features.Conversations.GetUnreadMessages;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Conversations;

public sealed class UnreadMessagesTests : IntegrationTestBase
{
    private const string UnreadUrl = "/api/v1/members/me/conversations/unread";
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
    private readonly Member _buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
    private readonly Member _secondBuyer = Member.Create("Mihai Popa", "mihai@student.ro", CreatedAt).Value;
    private readonly Member _otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
    private readonly Listing _listing;

    public UnreadMessagesTests()
    {
        _listing = Listing.Create(_seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        _listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
    }

    [Fact]
    public async Task Given_BuyerStartedConversation_When_SellerGetsUnread_Then_Returns1MessageIn1Conversation()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await StartConversationAsync(_buyer, "Buna, mai este disponibil?");
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(1, 1));

        await AssertMarksAsync(conversationId, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_BuyerStartedConversation_When_BuyerGetsUnread_Then_ReturnsZero()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await StartConversationAsync(_buyer, "Buna, mai este disponibil?");
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(0, 0));

        await AssertMarksAsync(conversationId, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_MemberWithoutConversations_When_GettingUnread_Then_ReturnsZero()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing, SeededConversation());
        AuthenticateAs(_otherMember.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(0, 0));

        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Given_BuyerSentThreeMessages_When_SellerGetsUnread_Then_Returns3MessagesIn1Conversation()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(3, 1));

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 0);
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    public async Task Given_BuyerSentThreeMessages_When_GettingMyConversations_Then_ItemHasUnreadCountOfTheReader(
        bool asSeller, int expectedUnread)
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        AuthenticateAs(asSeller ? _seller.Id : _buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(conversationId);
        item.MessageCount.Should().Be(3);
        item.UnreadCount.Should().Be(expectedUnread);

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 0);
    }

    [Fact]
    public async Task Given_BuyerSentThreeMessages_When_SellerGetsConversation_Then_UnreadCountIs3AndNothingIsMarked()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{conversationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetConversationResponse>(Json);
        body!.Messages.Should().HaveCount(3);
        body.UnreadCount.Should().Be(3);

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 0);
    }

    [Fact]
    public async Task Given_ThreeUnreadMessages_When_SellerMarksConversationRead_Then_Returns204AndMovesOnlySellerMark()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/conversations/{conversationId}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 3);
    }

    [Fact]
    public async Task Given_SellerReadConversation_When_SellerGetsUnread_Then_ReturnsZero()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        await MarkReadAsync(_seller, conversationId);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(0, 0));

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 3);
    }

    [Fact]
    public async Task Given_SellerReadConversation_When_SellerGetsMyConversations_Then_ItemUnreadCountIs0()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        await MarkReadAsync(_seller, conversationId);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(conversationId);
        item.UnreadCount.Should().Be(0);

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 3);
    }

    [Fact]
    public async Task Given_SellerReadAndAnswered_When_BuyerGetsUnread_Then_Returns1MessageIn1Conversation()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        await MarkReadAsync(_seller, conversationId);
        await SendAsync(_seller, conversationId, "Da, inca e. Poti sa vii maine.");
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(1, 1));

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 4);
    }

    [Fact]
    public async Task Given_SellerReadAndAnswered_When_SellerGetsUnread_Then_ReturnsZero()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing);
        var conversationId = await BuyerSendsThreeMessagesAsync();
        await MarkReadAsync(_seller, conversationId);
        await SendAsync(_seller, conversationId, "Da, inca e. Poti sa vii maine.");
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(0, 0));

        await AssertMarksAsync(conversationId, buyerMark: 3, sellerMark: 4);
    }

    [Fact]
    public async Task Given_SellerAnswered_When_BuyerMarksConversationRead_Then_Returns204AndMovesOnlyBuyerMark()
    {
        // Arrange
        var conversation = SeededConversation();
        conversation.Send(_seller.Id, "Da, inca e.", CreatedAt.AddMinutes(5));
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/conversations/{conversation.Id}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await AssertMarksAsync(conversation.Id, buyerMark: 2, sellerMark: 2);
    }

    [Fact]
    public async Task Given_TwoConversationsWithUnreadMessages_When_SellerGetsUnread_Then_Returns2Conversations()
    {
        // Arrange
        var first = SeededConversation();
        first.Send(_buyer.Id, "Pot sa vin maine la camin?", CreatedAt.AddMinutes(1));
        var second = Conversation.Start(_secondBuyer.Id, _listing, "Buna, il mai vinzi?", CreatedAt.AddMinutes(2)).Value;
        await SeedAsync(_seller, _buyer, _secondBuyer, _otherMember, _listing, first, second);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(3, 2));

        await AssertMarksAsync(first.Id, buyerMark: 2, sellerMark: 0);
        await AssertMarksAsync(second.Id, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_TwoConversationsAndOneRead_When_SellerGetsUnread_Then_TheOtherIsStillCounted()
    {
        // Arrange
        var first = SeededConversation();
        first.Send(_buyer.Id, "Pot sa vin maine la camin?", CreatedAt.AddMinutes(1));
        var second = Conversation.Start(_secondBuyer.Id, _listing, "Buna, il mai vinzi?", CreatedAt.AddMinutes(2)).Value;
        await SeedAsync(_seller, _buyer, _secondBuyer, _otherMember, _listing, first, second);
        await MarkReadAsync(_seller, first.Id);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetUnreadMessagesResponse>(Json);
        body.Should().Be(new GetUnreadMessagesResponse(1, 1));

        await AssertMarksAsync(first.Id, buyerMark: 2, sellerMark: 2);
        await AssertMarksAsync(second.Id, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_ConversationAlreadyRead_When_MarkingReadAgain_Then_Returns204AndRowIsUnchanged()
    {
        // Arrange
        var conversation = SeededConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        await MarkReadAsync(_seller, conversation.Id);
        var before = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/conversations/{conversation.Id}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var after = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        after.Should().BeEquivalentTo(before, options => options.Excluding(c => c.Messages));
        after.SellerLastReadNumber.Should().Be(1);
        after.BuyerLastReadNumber.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownConversation_When_MarkingRead_Then_Returns404()
    {
        // Arrange
        var conversation = SeededConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        AuthenticateAs(_seller.Id);
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/v1/conversations/{unknownId}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertMarksAsync(conversation.Id, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_ThirdMember_When_MarkingRead_Then_Returns403()
    {
        // Arrange
        var conversation = SeededConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        AuthenticateAs(_otherMember.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/conversations/{conversation.Id}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotParticipant");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertMarksAsync(conversation.Id, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_Admin_When_MarkingRead_Then_Returns403()
    {
        // Arrange
        var conversation = SeededConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/conversations/{conversation.Id}/read", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotParticipant");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertMarksAsync(conversation.Id, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_NoToken_When_MarkingRead_Then_Returns401()
    {
        // Arrange
        var conversation = SeededConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        var url = $"/api/v1/conversations/{conversation.Id}/read";

        // Act
        var response = await Client.PostAsync(url, content: null);

        // Assert
        await AssertUnauthorizedAsync(response, url);
        await AssertMarksAsync(conversation.Id, buyerMark: 1, sellerMark: 0);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingUnread_Then_Returns401()
    {
        // Arrange
        var conversation = SeededConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);

        // Act
        var response = await Client.GetAsync(UnreadUrl);

        // Assert
        await AssertUnauthorizedAsync(response, UnreadUrl);
        await AssertMarksAsync(conversation.Id, buyerMark: 1, sellerMark: 0);
    }

    // The buyer's first message, built in the domain: the buyer's mark is 1, the seller's 0.
    private Conversation SeededConversation() =>
        Conversation.Start(_buyer.Id, _listing, "Buna, mai este disponibil?", CreatedAt).Value;

    // The buyer starts the conversation through the API and sends two more messages: 3 unread for the seller.
    private async Task<Guid> BuyerSendsThreeMessagesAsync()
    {
        var conversationId = await StartConversationAsync(_buyer, "Buna, mai este disponibil?");
        await SendAsync(_buyer, conversationId, "Pot sa vin maine la camin?");
        await SendAsync(_buyer, conversationId, "Il iau daca e in stare buna.");
        return conversationId;
    }

    private async Task<Guid> StartConversationAsync(Member buyer, string text)
    {
        AuthenticateAs(buyer.Id);
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest(text), Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        return body!.ConversationId;
    }

    private async Task SendAsync(Member sender, Guid conversationId, string text)
    {
        AuthenticateAs(sender.Id);
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages", new SendMessageRequest(text), Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task MarkReadAsync(Member reader, Guid conversationId)
    {
        AuthenticateAs(reader.Id);
        var response = await Client.PostAsync($"/api/v1/conversations/{conversationId}/read", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task AssertMarksAsync(Guid conversationId, int buyerMark, int sellerMark)
    {
        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync(c => c.Id == conversationId));
        saved.BuyerLastReadNumber.Should().Be(buyerMark);
        saved.SellerLastReadNumber.Should().Be(sellerMark);
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response, string url)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(url);
        problem.TraceId.Should().NotBeEmpty();
    }
}
