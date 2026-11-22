using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Conversations;

public sealed class SendMessageTests : IntegrationTestBase
{
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_Seller_When_AnsweringInConversation_Then_Returns201AndSavesMessage()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(seller.Id);
        var request = new SendMessageRequest("  Da, inca e.  ");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.ConversationId.Should().Be(conversation.Id);
        body.Number.Should().Be(2);
        body.SenderId.Should().Be(seller.Id);
        body.Text.Should().Be("Da, inca e.");
        body.SentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/conversations/{conversation.Id}");

        var message = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync(m => m.Id == body.Id));
        message.ConversationId.Should().Be(conversation.Id);
        message.Number.Should().Be(2);
        message.SenderId.Should().Be(seller.Id);
        message.Text.Should().Be("Da, inca e.");

        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        saved.LastMessageAt.Should().Be(body.SentAt);

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        messageCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_ConversationWithAnswer_When_BuyerAnswersAgain_Then_Returns201WithNumber3()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        conversation.Send(seller.Id, "Da, inca e.", CreatedAt.AddMinutes(5));
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Pot sa vin maine?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.ConversationId.Should().Be(conversation.Id);
        body.Number.Should().Be(3);
        body.SenderId.Should().Be(buyer.Id);
        body.Text.Should().Be("Pot sa vin maine?");

        var message = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync(m => m.Id == body.Id));
        message.Number.Should().Be(3);
        message.SenderId.Should().Be(buyer.Id);

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        messageCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_AnotherMember_When_SendingMessage_Then_Returns403AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, otherMember, listing, conversation);
        AuthenticateAs(otherMember.Id);
        var request = new SendMessageRequest("Si eu il vreau.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotParticipant");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertOnlyFirstMessageAsync(conversation);
    }

    [Fact]
    public async Task Given_Admin_When_SendingMessage_Then_Returns403AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(AdminId, "Admin");
        var request = new SendMessageRequest("Mesaj de la administrator.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotParticipant");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertOnlyFirstMessageAsync(conversation);
    }

    [Fact]
    public async Task Given_EmptyText_When_SendingMessage_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(seller.Id);
        var request = new SendMessageRequest("");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Empty");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertOnlyFirstMessageAsync(conversation);
    }

    [Fact]
    public async Task Given_UnknownConversation_When_SendingMessage_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(seller.Id);
        var request = new SendMessageRequest("Da, inca e.");
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{unknownId}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertOnlyFirstMessageAsync(conversation);
    }

    [Fact]
    public async Task Given_NoToken_When_SendingMessage_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        var request = new SendMessageRequest("Da, inca e.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/conversations/{conversation.Id}/messages");
        problem.TraceId.Should().NotBeEmpty();

        await AssertOnlyFirstMessageAsync(conversation);
    }

    // A published listing of this seller.
    private static Listing PublishedListing(Member seller)
    {
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        return listing;
    }

    // The seeded conversation still has only its first message and its LastMessageAt did not move.
    private async Task AssertOnlyFirstMessageAsync(Conversation conversation)
    {
        var message = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        message.Id.Should().Be(conversation.Messages[0].Id);

        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        saved.LastMessageAt.Should().Be(CreatedAt);
    }
}
