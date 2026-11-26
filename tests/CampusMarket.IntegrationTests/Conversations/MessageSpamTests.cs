using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.Api.Features.Conversations.GetMyConversations;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Conversations;

// Every message goes through the real message spam model (ml-models/message-spam.zip) and MessageSpamRules.
// The texts are the ones measured with the committed model: never assert the model's exact probability.
public sealed class MessageSpamTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private const string NormalText = "Buna, mai este disponibil? Pot sa vin maine la camin.";
    private const string SuspiciousByRuleText = "Uite modelul pe www.casio.com, e exact ala";
    private const string BlockedText =
        "Am platit prin Cargus, intra pe cargus-plati.com si introdu cardul ca sa primesti banii";

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
    private readonly Member _buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
    private readonly Member _otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
    private readonly Listing _listing;

    public MessageSpamTests()
    {
        _listing = Listing.Create(_seller.Id, "Calculator Casio fx-991", Category.Electronics, Condition.Good, 80m, CreatedAt).Value;
        _listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
    }

    private MessageSpamOptions SpamOptions => Factory.Services.GetRequiredService<IOptions<MessageSpamOptions>>().Value;

    private Conversation StartedConversation() =>
        Conversation.Start(_buyer.Id, _listing, "Mai e disponibil?", CreatedAt).Value;

    // ---------- Start conversation ----------

    [Theory]
    [InlineData("Buna, mai este disponibil? Pot sa vin maine la camin.")]
    [InlineData("Suna-ma la 0745 123 456 cand ajungi")]
    [InlineData("Codul de la interfon e 23, urci la etajul 2")]
    [InlineData("Nu cer avans, platesti cand il vezi")]
    public async Task Given_NormalText_When_StartingConversation_Then_Returns201NotSuspicious(string text)
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest(text), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeFalse();
        body.Number.Should().Be(1);
        body.Text.Should().Be(text);

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.IsSuspicious.Should().BeFalse();
        saved.SpamProbability.Should().NotBeNull();
        saved.SpamProbability!.Value.Should().BeGreaterThanOrEqualTo(0)
            .And.BeLessThan(SpamOptions.Threshold, "a normal message is under the warning threshold of the model");
        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(1);
    }

    [Theory]
    [InlineData("Uite modelul pe www.casio.com, e exact ala")]
    [InlineData("Ti-a venit un SMS cu un cod? Dicteaza-mi-l sa confirm livrarea.")]
    public async Task Given_TextCaughtByARule_When_StartingConversation_Then_Returns201Suspicious(string text)
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest(text), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeTrue();

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        saved.IsSuspicious.Should().BeTrue();
        saved.SpamProbability!.Value.Should().BeLessThan(SpamOptions.Threshold,
            "the text must be caught only by a rule of MessageSpamRules, not by the model");
    }

    [Fact]
    public async Task Given_TextCaughtByTheModel_When_StartingConversation_Then_Returns201Suspicious()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);
        var request = new SendMessageRequest("Angajam studenti, lucru de acasa, 300 lei pe saptamana");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeTrue();

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        saved.IsSuspicious.Should().BeTrue();
        saved.SpamProbability!.Value.Should().BeGreaterThanOrEqualTo(SpamOptions.Threshold)
            .And.BeLessThan(SpamOptions.BlockThreshold);
    }

    [Theory]
    [InlineData("Am platit prin Cargus, intra pe cargus-plati.com si introdu cardul ca sa primesti banii")]
    [InlineData("Investeste 500 lei in bitcoin si ii dublezi in 24 de ore, profit garantat")]
    [InlineData("Castiga 500 lei pe zi de acasa, scrie-mi si iti explic metoda")]
    public async Task Given_BlockedText_When_StartingConversation_Then_Returns400AndSavesNothing(string text)
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest(text), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Spam");
        error.Type.Should().Be(ErrorType.Validation);

        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Message>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_ExistingConversation_When_StartingWithBlockedText_Then_Returns400AndConversationUnchanged()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest(BlockedText), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Spam");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertConversationUnchangedAsync(conversation, messageCount: 1);
    }

    // ---------- Send message ----------

    [Theory]
    [InlineData("Buna, mai este disponibil? Pot sa vin maine la camin.")]
    [InlineData("Suna-ma la 0745 123 456 cand ajungi")]
    [InlineData("Codul de la interfon e 23, urci la etajul 2")]
    [InlineData("Nu cer avans, platesti cand il vezi")]
    public async Task Given_NormalText_When_SendingMessage_Then_Returns201NotSuspicious(string text)
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest(text), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeFalse();
        body.Number.Should().Be(2);

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync(m => m.Id == body.Id));
        saved.IsSuspicious.Should().BeFalse();
        saved.SpamProbability!.Value.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(SpamOptions.Threshold);
    }

    [Theory]
    [InlineData("Uite modelul pe www.casio.com, e exact ala")]
    [InlineData("Ti-a venit un SMS cu un cod? Dicteaza-mi-l sa confirm livrarea.")]
    public async Task Given_TextCaughtByARule_When_SendingMessage_Then_Returns201Suspicious(string text)
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest(text), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeTrue();

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync(m => m.Id == body.Id));
        saved.IsSuspicious.Should().BeTrue();
        saved.SpamProbability!.Value.Should().BeLessThan(SpamOptions.Threshold,
            "the text must be caught only by a rule of MessageSpamRules, not by the model");
    }

    [Fact]
    public async Task Given_TextCaughtByTheModel_When_SendingMessage_Then_Returns201Suspicious()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);
        var request = new SendMessageRequest("Angajam studenti, lucru de acasa, 300 lei pe saptamana");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.IsSuspicious.Should().BeTrue();

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync(m => m.Id == body.Id));
        saved.IsSuspicious.Should().BeTrue();
        saved.SpamProbability!.Value.Should().BeGreaterThanOrEqualTo(SpamOptions.Threshold)
            .And.BeLessThan(SpamOptions.BlockThreshold);
    }

    [Theory]
    [InlineData("Am platit prin Cargus, intra pe cargus-plati.com si introdu cardul ca sa primesti banii")]
    [InlineData("Investeste 500 lei in bitcoin si ii dublezi in 24 de ore, profit garantat")]
    [InlineData("Castiga 500 lei pe zi de acasa, scrie-mi si iti explic metoda")]
    public async Task Given_BlockedText_When_SendingMessage_Then_Returns400AndConversationUnchanged(string text)
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest(text), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Spam");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertConversationUnchangedAsync(conversation, messageCount: 1);
    }

    [Fact]
    public async Task Given_ABlockedMessageBefore_When_SendingNormalMessage_Then_Returns201WithNextNumber()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);
        var url = $"/api/v1/conversations/{conversation.Id}/messages";
        var blocked = await Client.PostAsJsonAsync(url, new SendMessageRequest(BlockedText), Json);
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Act
        var response = await Client.PostAsJsonAsync(url, new SendMessageRequest(NormalText), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.Number.Should().Be(2);
        body.IsSuspicious.Should().BeFalse();

        var numbers = await QueryDbAsync(db => db.Set<Message>().AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id).OrderBy(m => m.Number).Select(m => m.Number).ToListAsync());
        numbers.Should().Equal(1, 2);
        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        saved.LastMessageAt.Should().Be(body.SentAt.ToMicroseconds());
    }

    // ---------- Read side ----------

    [Fact]
    public async Task Given_NormalAndSuspiciousMessage_When_BuyerGetsConversation_Then_EachMessageHasItsFlag() =>
        await AssertFlagsSeenByAsync(_buyer);

    [Fact]
    public async Task Given_NormalAndSuspiciousMessage_When_SellerGetsConversation_Then_EachMessageHasItsFlag() =>
        await AssertFlagsSeenByAsync(_seller);

    [Fact]
    public async Task Given_MessageWithoutSpamCheck_When_GettingConversation_Then_ReadsAsNotSuspicious()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetConversationResponse>(Json);
        body!.Messages.Should().ContainSingle().Which.IsSuspicious.Should().BeFalse();

        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        saved.SpamProbability.Should().BeNull();
        saved.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public async Task Given_LastMessageSuspicious_When_GettingMyConversations_Then_LastMessageIsSuspiciousTrue()
    {
        // Arrange
        var conversation = StartedConversation();
        conversation.Send(_seller.Id, SuspiciousByRuleText, CreatedAt.AddMinutes(5)).Value.RecordSpamCheck(0.01, isSuspicious: true);
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.LastMessage.Should().Be(SuspiciousByRuleText);
        item.LastMessageIsSuspicious.Should().BeTrue();

        await AssertConversationUnchangedAsync(conversation, messageCount: 2);
    }

    [Fact]
    public async Task Given_NormalMessageAfterSuspiciousOne_When_GettingMyConversations_Then_LastMessageIsSuspiciousFalse()
    {
        // Arrange
        var conversation = StartedConversation();
        conversation.Send(_seller.Id, SuspiciousByRuleText, CreatedAt.AddMinutes(5)).Value.RecordSpamCheck(0.01, isSuspicious: true);
        conversation.Send(_buyer.Id, NormalText, CreatedAt.AddMinutes(10)).Value.RecordSpamCheck(0.02, isSuspicious: false);
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.LastMessage.Should().Be(NormalText);
        item.LastMessageIsSuspicious.Should().BeFalse();
        item.MessageCount.Should().Be(3);

        await AssertConversationUnchangedAsync(conversation, messageCount: 3);
    }

    // ---------- The earlier errors still win ----------

    [Fact]
    public async Task Given_EmptyText_When_StartingConversation_Then_Returns400MessageEmpty()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest("   "), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Empty");
        error.Type.Should().Be(ErrorType.Validation);

        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Message>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyText_When_SendingMessage_Then_Returns400MessageEmpty()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest("   "), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Empty");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertConversationUnchangedAsync(conversation, messageCount: 1);
    }

    [Fact]
    public async Task Given_BlockedMemberWithBlockedText_When_StartingConversation_Then_Returns403MemberBlocked()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _listing, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{_listing.Id}/conversations", new SendMessageRequest(BlockedText), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.Blocked");
        error.Type.Should().Be(ErrorType.Forbidden);

        (await QueryDbAsync(db => db.Set<Conversation>().CountAsync())).Should().Be(0);
        (await QueryDbAsync(db => db.Set<Message>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_BlockedMemberWithBlockedText_When_SendingMessage_Then_Returns403MemberBlocked()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _listing, conversation, MemberBlock.Create(_seller.Id, _buyer.Id, CreatedAt).Value);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest(BlockedText), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.Blocked");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertConversationUnchangedAsync(conversation, messageCount: 1);
    }

    [Fact]
    public async Task Given_StrangerWithBlockedText_When_SendingMessage_Then_Returns403NotParticipant()
    {
        // Arrange
        var conversation = StartedConversation();
        await SeedAsync(_seller, _buyer, _otherMember, _listing, conversation);
        AuthenticateAs(_otherMember.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest(BlockedText), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotParticipant");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertConversationUnchangedAsync(conversation, messageCount: 1);
    }

    // One normal and one suspicious message: the member sees false for the first and true for the second.
    private async Task AssertFlagsSeenByAsync(Member member)
    {
        // Arrange
        var conversation = StartedConversation();
        conversation.Messages[0].RecordSpamCheck(0.01, isSuspicious: false);
        conversation.Send(_seller.Id, SuspiciousByRuleText, CreatedAt.AddMinutes(5)).Value.RecordSpamCheck(0.01, isSuspicious: true);
        await SeedAsync(_seller, _buyer, _listing, conversation);
        AuthenticateAs(member.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetConversationResponse>(Json);
        body!.Messages.Select(m => (m.Number, m.IsSuspicious)).Should().Equal((1, false), (2, true));

        await AssertConversationUnchangedAsync(conversation, messageCount: 2);
    }

    // The conversation still has the same messages and its LastMessageAt did not move.
    private async Task AssertConversationUnchangedAsync(Conversation conversation, int messageCount)
    {
        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(conversation.Id);
        saved.LastMessageAt.Should().Be(conversation.LastMessageAt.ToMicroseconds());

        var count = await QueryDbAsync(db => db.Set<Message>().CountAsync(m => m.ConversationId == conversation.Id));
        count.Should().Be(messageCount);
    }
}
