using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Conversations;

public sealed class GetConversationTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
    private readonly Member _buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
    private readonly Member _otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
    private readonly Listing _listing;
    private readonly Conversation _conversation;

    public GetConversationTests()
    {
        _listing = Listing.Create(_seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        _listing.Publish();

        // Three messages: buyer, seller, buyer.
        _conversation = Conversation.Start(_buyer.Id, _listing, "Mai e disponibil?", CreatedAt).Value;
        _conversation.Send(_seller.Id, "Da, inca e.", CreatedAt.AddMinutes(5));
        _conversation.Send(_buyer.Id, "Pot sa vin maine?", CreatedAt.AddMinutes(10));
    }

    [Fact]
    public async Task Given_Buyer_When_GettingConversation_Then_Returns200WithEveryMessage()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing, _conversation);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{_conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetConversationResponse>(Json);
        body.Should().BeEquivalentTo(ExpectedResponse(), options => options.WithStrictOrdering());

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_Seller_When_GettingConversation_Then_Returns200WithEveryMessage()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing, _conversation);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{_conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetConversationResponse>(Json);
        body.Should().BeEquivalentTo(ExpectedResponse(), options => options.WithStrictOrdering());

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_AnotherMember_When_GettingConversation_Then_Returns403()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing, _conversation);
        AuthenticateAs(_otherMember.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{_conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotParticipant");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_UnknownId_When_GettingConversation_Then_Returns404()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing, _conversation);
        AuthenticateAs(_buyer.Id);
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{unknownId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_NoToken_When_GettingConversation_Then_Returns401()
    {
        // Arrange
        await SeedAsync(_seller, _buyer, _otherMember, _listing, _conversation);

        // Act
        var response = await Client.GetAsync($"/api/v1/conversations/{_conversation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/conversations/{_conversation.Id}");
        problem.TraceId.Should().NotBeEmpty();

        await AssertDatabaseUnchangedAsync();
    }

    // The seeded conversation as the API should return it, the messages in number order.
    private GetConversationResponse ExpectedResponse() => new(
        _conversation.Id,
        _listing.Id,
        "Manual POO",
        _seller.Id,
        "Ion Ionescu",
        _buyer.Id,
        "Ana Popescu",
        CreatedAt,
        [
            new ConversationMessage(_conversation.Messages[0].Id, 1, _buyer.Id, "Mai e disponibil?", CreatedAt),
            new ConversationMessage(_conversation.Messages[1].Id, 2, _seller.Id, "Da, inca e.", CreatedAt.AddMinutes(5)),
            new ConversationMessage(_conversation.Messages[2].Id, 3, _buyer.Id, "Pot sa vin maine?", CreatedAt.AddMinutes(10))
        ]);

    // Still one conversation with its three messages, and the last message time did not move.
    private async Task AssertDatabaseUnchangedAsync()
    {
        var saved = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(_conversation.Id);
        saved.LastMessageAt.Should().Be(CreatedAt.AddMinutes(10));

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        messageCount.Should().Be(3);
    }
}
