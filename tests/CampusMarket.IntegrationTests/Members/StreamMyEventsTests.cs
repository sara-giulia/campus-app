using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Members;

public sealed class StreamMyEventsTests : IntegrationTestBase
{
    private const string EventsUrl = "/api/v1/members/me/events";
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan EventExpected = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NoEventExpected = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Given_NoToken_When_OpeningEventStream_Then_Returns401()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(seller);

        // Act
        var response = await Client.GetAsync(EventsUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be(401);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(EventsUrl);
        problem.TraceId.Should().NotBeNullOrEmpty();

        var members = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        members.Should().Be(1);
        var notifications = await QueryDbAsync(db => db.Set<Notification>().CountAsync());
        notifications.Should().Be(0);
    }

    [Fact]
    public async Task Given_Token_When_OpeningEventStream_Then_Returns200AndFirstEventIsPing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        await SeedAsync(seller);

        // Act
        using var stream = await OpenStreamAsync(Factory, seller.Id);

        // Assert
        stream.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        stream.Response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var first = await ReadEventAsync(stream.Reader, EventExpected);
        first!.Name.Should().Be("ping");
        first.Data.Type.Should().Be("ping");
        first.Data.ConversationId.Should().BeNull();

        var notifications = await QueryDbAsync(db => db.Set<Notification>().CountAsync());
        notifications.Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerStreamOpen_When_BuyerReservesListing_Then_SellerGetsNotificationEvent()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        await SeedAsync(seller, buyer, listing);
        using var stream = await OpenStreamAsync(Factory, seller.Id);
        await ReadEventAsync(stream.Reader, EventExpected);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var received = await ReadEventAsync(stream.Reader, EventExpected);
        received!.Name.Should().Be("notification");
        received.Data.Type.Should().Be("notification");
        received.Data.ConversationId.Should().BeNull();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Should().ContainSingle(r => r.MemberId == buyer.Id);

        var notifications = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking().ToListAsync());
        notifications.Should().ContainSingle().Which.MemberId.Should().Be(seller.Id);
    }

    [Fact]
    public async Task Given_BuyerStreamOpen_When_SellerSendsMessage_Then_BuyerGetsMessageEvent()
    {
        // Arrange
        var (seller, buyer, conversation) = await SeedConversationAsync();
        using var stream = await OpenStreamAsync(Factory, buyer.Id);
        await ReadEventAsync(stream.Reader, EventExpected);
        AuthenticateAs(seller.Id);

        // Act
        var response = await SendMessageAsync(conversation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var received = await ReadEventAsync(stream.Reader, EventExpected);
        received!.Name.Should().Be("message");
        received.Data.Type.Should().Be("message");
        received.Data.ConversationId.Should().Be(conversation.Id);

        var messages = await QueryDbAsync(db => db.Set<Message>().CountAsync(m => m.ConversationId == conversation.Id));
        messages.Should().Be(2);
    }

    [Fact]
    public async Task Given_SellerStreamOpen_When_SellerSendsMessage_Then_SellerGetsNoEvent()
    {
        // Arrange
        var (seller, _, conversation) = await SeedConversationAsync();
        using var stream = await OpenStreamAsync(Factory, seller.Id);
        await ReadEventAsync(stream.Reader, EventExpected);
        AuthenticateAs(seller.Id);

        // Act
        var response = await SendMessageAsync(conversation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var received = await ReadEventAsync(stream.Reader, NoEventExpected);
        received.Should().BeNull();

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json);
        var saved = await QueryDbAsync(db => db.Set<Message>().AsNoTracking().SingleAsync(m => m.Id == body!.Id));
        saved.SenderId.Should().Be(seller.Id);
    }

    [Fact]
    public async Task Given_OutsiderStreamOpen_When_SellerSendsMessage_Then_OutsiderGetsNoEvent()
    {
        // Arrange
        var (seller, _, conversation) = await SeedConversationAsync();
        using var stream = await OpenStreamAsync(Factory, OtherStudentId);
        await ReadEventAsync(stream.Reader, EventExpected);
        AuthenticateAs(seller.Id);

        // Act
        var response = await SendMessageAsync(conversation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var received = await ReadEventAsync(stream.Reader, NoEventExpected);
        received.Should().BeNull();

        var messages = await QueryDbAsync(db => db.Set<Message>().CountAsync(m => m.ConversationId == conversation.Id));
        messages.Should().Be(2);
    }

    [Fact]
    public async Task Given_TwoBuyerStreamsOpen_When_SellerSendsMessage_Then_BothGetMessageEvent()
    {
        // Arrange
        var (seller, buyer, conversation) = await SeedConversationAsync();
        using var first = await OpenStreamAsync(Factory, buyer.Id);
        using var second = await OpenStreamAsync(Factory, buyer.Id);
        await ReadEventAsync(first.Reader, EventExpected);
        await ReadEventAsync(second.Reader, EventExpected);
        AuthenticateAs(seller.Id);

        // Act
        var response = await SendMessageAsync(conversation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var onFirst = await ReadEventAsync(first.Reader, EventExpected);
        onFirst!.Name.Should().Be("message");
        onFirst.Data.ConversationId.Should().Be(conversation.Id);

        var onSecond = await ReadEventAsync(second.Reader, EventExpected);
        onSecond!.Name.Should().Be("message");
        onSecond.Data.ConversationId.Should().Be(conversation.Id);

        var messages = await QueryDbAsync(db => db.Set<Message>().CountAsync(m => m.ConversationId == conversation.Id));
        messages.Should().Be(2);
    }

    [Fact]
    public async Task Given_HeartbeatOf1Second_When_WaitingAfterFirstPing_Then_SecondPingArrives()
    {
        // Arrange
        await using var fastHeartbeat = WithHeartbeat("1");
        await EnsureDatabaseAsync(fastHeartbeat);
        using var stream = await OpenStreamAsync(fastHeartbeat, OtherStudentId);
        await ReadEventAsync(stream.Reader, EventExpected);

        // Act
        var received = await ReadEventAsync(stream.Reader, EventExpected);

        // Assert
        received!.Name.Should().Be("ping");
        received.Data.Type.Should().Be("ping");
        received.Data.ConversationId.Should().BeNull();

        var notifications = await QueryDbAsync(db => db.Set<Notification>().CountAsync());
        notifications.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    public async Task Given_HeartbeatOutOfRange_When_StartingTheApp_Then_ThrowsOptionsValidationException(string seconds)
    {
        // Arrange
        await using var invalid = WithHeartbeat(seconds);

        // Act
        Action act = () => invalid.CreateClient();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*LiveEvents:HeartbeatSeconds must be between 1 and 300.*");

        var members = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        members.Should().Be(0);
    }

    private WebApplicationFactory<Program> WithHeartbeat(string seconds) =>
        Factory.WithWebHostBuilder(builder => builder.UseSetting("LiveEvents:HeartbeatSeconds", seconds));

    private static async Task EnsureDatabaseAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    private static Listing PublishedListing(Member seller)
    {
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        return listing;
    }

    private async Task<(Member Seller, Member Buyer, Conversation Conversation)> SeedConversationAsync()
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller);
        var conversation = Conversation.Start(buyer.Id, listing, "Buna, mai e valabil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        return (seller, buyer, conversation);
    }

    private Task<HttpResponseMessage> SendMessageAsync(Conversation conversation) =>
        Client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/messages", new SendMessageRequest("Da, inca e."), Json);

    // A separate client for the stream, so the normal Client stays free for the Act.
    private static async Task<LiveStream> OpenStreamAsync(WebApplicationFactory<Program> factory, Guid memberId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(memberId, "Student"));
        var response = await client.GetAsync(EventsUrl, HttpCompletionOption.ResponseHeadersRead);
        var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        return new LiveStream(client, response, reader);
    }

    // Reads one event (lines until the empty line that ends it); null when the timeout runs out first.
    private static async Task<ReceivedEvent?> ReadEventAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        string? name = null;
        string? data = null;

        try
        {
            var line = await reader.ReadLineAsync(cts.Token);
            while (!string.IsNullOrEmpty(line))
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                    name = line["event:".Length..].Trim();
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                    data = line["data:".Length..].Trim();

                line = await reader.ReadLineAsync(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (name is null || data is null)
            return null;

        return new ReceivedEvent(name, JsonSerializer.Deserialize<LiveEventData>(data, Json)!);
    }

    private sealed record LiveEventData(string Type, Guid? ConversationId);

    private sealed record ReceivedEvent(string Name, LiveEventData Data);

    // Keeps the stream's client and response alive until the test ends, then closes the connection.
    private sealed class LiveStream(HttpClient client, HttpResponseMessage response, StreamReader reader) : IDisposable
    {
        public HttpResponseMessage Response { get; } = response;
        public StreamReader Reader { get; } = reader;

        public void Dispose()
        {
            Reader.Dispose();
            Response.Dispose();
            client.Dispose();
        }
    }
}
