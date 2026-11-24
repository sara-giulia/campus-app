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

public sealed class StartConversationTests : IntegrationTestBase
{
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_PublishedListing_When_BuyerStartsConversation_Then_Returns201AndSavesConversation()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("  Mai e disponibil?  ");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.ConversationId.Should().NotBeEmpty();
        body.Id.Should().NotBeEmpty();
        body.Number.Should().Be(1);
        body.SenderId.Should().Be(buyer.Id);
        body.Text.Should().Be("Mai e disponibil?");
        body.SentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/conversations/{body.ConversationId}");

        var conversation = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        conversation.Id.Should().Be(body.ConversationId);
        conversation.ListingId.Should().Be(listing.Id);
        conversation.SellerId.Should().Be(seller.Id);
        conversation.BuyerId.Should().Be(buyer.Id);

        var message = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync());
        message.Id.Should().Be(body.Id);
        message.ConversationId.Should().Be(body.ConversationId);
        message.Number.Should().Be(1);
        message.SenderId.Should().Be(buyer.Id);
        message.Text.Should().Be("Mai e disponibil?");
    }

    [Fact]
    public async Task Given_ExistingConversation_When_BuyerWritesAgain_Then_Returns201WithNextNumber()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Pot sa vin maine?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.ConversationId.Should().Be(conversation.Id);
        body.Number.Should().Be(2);
        body.SenderId.Should().Be(buyer.Id);
        body.Text.Should().Be("Pot sa vin maine?");
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/conversations/{conversation.Id}");

        var conversationCount = await QueryDbAsync(db => db.Set<Conversation>().CountAsync());
        conversationCount.Should().Be(1);

        var messages = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().OrderBy(m => m.Number).ToListAsync());
        messages.Should().HaveCount(2);
        messages[1].Id.Should().Be(body.Id);
        messages[1].Number.Should().Be(2);
        messages[1].Text.Should().Be("Pot sa vin maine?");
    }

    [Fact]
    public async Task Given_ReservedListing_When_BuyerStartsConversation_Then_Returns201()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        listing.Reserve(buyer.Id, CreatedAt);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Cand ne vedem?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.Number.Should().Be(1);
        body.SenderId.Should().Be(buyer.Id);

        var conversation = await QueryDbAsync(db => db.Set<Conversation>().AsNoTracking().SingleAsync());
        conversation.Id.Should().Be(body.ConversationId);
        conversation.ListingId.Should().Be(listing.Id);

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        messageCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_Seller_When_StartingConversationAboutOwnListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, listing);
        AuthenticateAs(seller.Id);
        var request = new SendMessageRequest("Mai e disponibil?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.OwnListing");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_DraftListing_When_StartingConversation_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Mai e disponibil?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.ListingNotAvailable");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_SoldListingWithoutConversation_When_StartingConversation_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        SellTo(listing, buyer);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Mai e disponibil?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Conversation.ListingNotAvailable");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_SoldListingWithConversation_When_BuyerWritesAgain_Then_Returns201WithNextNumber()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        SellTo(listing, buyer);
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Multumesc, a ajuns cartea.");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        body!.ConversationId.Should().Be(conversation.Id);
        body.Number.Should().Be(2);
        body.Text.Should().Be("Multumesc, a ajuns cartea.");

        var conversationCount = await QueryDbAsync(db => db.Set<Conversation>().CountAsync());
        conversationCount.Should().Be(1);

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync(m => m.ConversationId == conversation.Id));
        messageCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_EmptyText_When_StartingConversation_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.Empty");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_TextOf1001Characters_When_StartingConversation_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest(new string('a', 1001));

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Message.TooLong");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_UnknownListing_When_StartingConversation_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, buyer, listing);
        AuthenticateAs(buyer.Id);
        var request = new SendMessageRequest("Mai e disponibil?");
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{unknownId}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_StartingConversation_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, listing);
        AuthenticateAs(OtherStudentId);
        var request = new SendMessageRequest("Mai e disponibil?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_NoToken_When_StartingConversation_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, listing);
        var request = new SendMessageRequest("Mai e disponibil?");

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/conversations", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/conversations");
        problem.TraceId.Should().NotBeEmpty();

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_ListingWithConversation_When_SellerDeletesListing_Then_Returns204AndConversationIsGone()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        conversation.Send(seller.Id, "Da, inca e.", CreatedAt.AddMinutes(5));
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var listingExists = await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == listing.Id));
        listingExists.Should().BeFalse();

        await AssertNothingSavedAsync();
    }

    // A published listing of this seller.
    private static Listing PublishedListing(Member seller)
    {
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        return listing;
    }

    // Reserves the listing for the buyer and confirms the sale.
    private static void SellTo(Listing listing, Member buyer)
    {
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        listing.ConfirmSale(reservation.Id, CreatedAt);
    }

    // No conversation and no message in the database.
    private async Task AssertNothingSavedAsync()
    {
        var conversationCount = await QueryDbAsync(db => db.Set<Conversation>().CountAsync());
        conversationCount.Should().Be(0);

        var messageCount = await QueryDbAsync(db => db.Set<Message>().CountAsync());
        messageCount.Should().Be(0);
    }
}
