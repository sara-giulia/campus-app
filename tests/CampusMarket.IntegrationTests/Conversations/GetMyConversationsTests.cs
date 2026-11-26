using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.GetMyConversations;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Conversations;

public sealed class GetMyConversationsTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
    private readonly Member _firstBuyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
    private readonly Member _secondBuyer = Member.Create("Mihai Popa", "mihai@student.ro", CreatedAt).Value;
    private readonly Member _otherMember = Member.Create("Maria Ionescu", "maria@student.ro", CreatedAt).Value;
    private readonly Listing _listing;
    private readonly Conversation _firstConversation;
    private readonly Conversation _secondConversation;

    public GetMyConversationsTests()
    {
        _listing = Listing.Create(_seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        _listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());

        // The first buyer writes first, but the seller's answer at +10 min makes it the latest conversation.
        _firstConversation = Conversation.Start(_firstBuyer.Id, _listing, "Mai e disponibil?", CreatedAt).Value;
        _firstConversation.Send(_seller.Id, "Da, inca e.", CreatedAt.AddMinutes(10));

        _secondConversation = Conversation.Start(_secondBuyer.Id, _listing, "Pot sa vin azi?", CreatedAt.AddMinutes(5)).Value;
    }

    [Fact]
    public async Task Given_SellerWithTwoConversations_When_GettingMyConversations_Then_Returns200LatestFirst()
    {
        // Arrange
        await SeedAllAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Should().BeEquivalentTo(
            new[]
            {
                new MyConversationItem(
                    _firstConversation.Id, _listing.Id, "Manual POO", _firstBuyer.Id, "Ana Popescu",
                    "Da, inca e.", _seller.Id, false, CreatedAt.AddMinutes(10), 2, 0),
                new MyConversationItem(
                    _secondConversation.Id, _listing.Id, "Manual POO", _secondBuyer.Id, "Mihai Popa",
                    "Pot sa vin azi?", _secondBuyer.Id, false, CreatedAt.AddMinutes(5), 1, 1)
            },
            options => options.WithStrictOrdering());

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_Buyer_When_GettingMyConversations_Then_Returns200WithSellerAsOtherMember()
    {
        // Arrange
        await SeedAllAsync();
        AuthenticateAs(_firstBuyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new MyConversationItem(
                _firstConversation.Id, _listing.Id, "Manual POO", _seller.Id, "Ion Ionescu",
                "Da, inca e.", _seller.Id, false, CreatedAt.AddMinutes(10), 2, 1));

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_MemberWithoutConversations_When_GettingMyConversations_Then_Returns200Empty()
    {
        // Arrange
        await SeedAllAsync();
        AuthenticateAs(_otherMember.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyConversationsResponse>(Json);
        body!.Count.Should().Be(0);
        body.Items.Should().BeEmpty();

        await AssertDatabaseUnchangedAsync();
    }

    [Fact]
    public async Task Given_NoToken_When_GettingMyConversations_Then_Returns401()
    {
        // Arrange
        await SeedAllAsync();

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/members/me/conversations");
        problem.TraceId.Should().NotBeEmpty();

        await AssertDatabaseUnchangedAsync();
    }

    private Task SeedAllAsync() => SeedAsync(
        _seller, _firstBuyer, _secondBuyer, _otherMember, _listing, _firstConversation, _secondConversation);

    // Still the two conversations with their three messages.
    private async Task AssertDatabaseUnchangedAsync()
    {
        var conversationCount = await QueryDbAsync(db => db.Set<Conversation>().CountAsync());
        conversationCount.Should().Be(2);

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        messageCount.Should().Be(3);
    }
}
