using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Domain.Views;
using CampusMarket.Api.Features.Views.GetListingStats;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Views;

public sealed class GetListingStatsTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");
    private readonly Member _third = NewMember("Maria Ionescu", "maria@student.ro");
    private readonly Member _admin = Member.Register("Admin Campus", "admin@student.ro", "hash", CreatedAt, MemberRole.Admin).Value;
    private readonly Listing _listing;
    private readonly Listing _other;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    public GetListingStatsTests()
    {
        _listing = ActiveListing(_seller.Id, "Manual POO");
        _other = ActiveListing(_seller.Id, "Manual Algebra");
    }

    // Noon UTC, n days before today.
    private DateTimeOffset DaysAgo(int n, int hour = 12) =>
        new(_today.AddDays(-n).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

    private static ListingView View(Listing listing, string visitor, DateTimeOffset at) =>
        ListingView.Create(listing.Id, $"v:{visitor}", at);

    private async Task SeedActivityAsync()
    {
        // Listing: views on three days (plus one outside 30 days), favorites on two days, messages in two
        // conversations, offers on two days (plus one outside 30 days).
        var conversation = Conversation.Start(_buyer.Id, _listing, "Mai e disponibil?", DaysAgo(1, 10)).Value;
        conversation.Send(_seller.Id, "Da, este.", DaysAgo(1, 11));
        conversation.Send(_buyer.Id, "Vin azi.", DaysAgo(0));
        var older = Conversation.Start(_third.Id, _listing, "Salut", DaysAgo(10)).Value;

        var offer = Offer.Make(_listing, _buyer.Id, 80m, DaysAgo(2)).Value;
        var counter = offer.Counter(_seller.Id, _listing, 90m, DaysAgo(0)).Value;
        var oldOffer = Offer.Make(_listing, _third.Id, 70m, DaysAgo(40)).Value;

        // Another listing of the same seller: its data must never appear in the stats of _listing.
        var otherConversation = Conversation.Start(_buyer.Id, _other, "Salut", DaysAgo(0)).Value;
        var otherOffer = Offer.Make(_other, _buyer.Id, 50m, DaysAgo(0)).Value;

        await SeedAsync(
            _seller, _buyer, _third, _admin, _listing, _other,
            View(_listing, "a", DaysAgo(0)), View(_listing, "b", DaysAgo(0)), View(_listing, "a", DaysAgo(2)),
            View(_listing, "a", DaysAgo(5)), View(_listing, "a", DaysAgo(40)), View(_other, "a", DaysAgo(0)),
            Favorite.Create(_buyer.Id, _listing, DaysAgo(3)).Value, Favorite.Create(_third.Id, _listing, DaysAgo(0)).Value,
            Favorite.Create(_buyer.Id, _other, DaysAgo(0)).Value,
            conversation, older, otherConversation,
            offer, counter, oldOffer, otherOffer);
    }

    private async Task<GetListingStatsResponse> GetStatsAsync(string query = "")
    {
        var response = await Client.GetAsync($"/api/v1/listings/{_listing.Id}/stats{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<GetListingStatsResponse>(Json))!;
    }

    private static ListingStatsDay Day(GetListingStatsResponse stats, DateOnly date) => stats.Daily.Single(d => d.Date == date);

    [Fact]
    public async Task Given_ActivityOnSeveralDays_When_SellerReadsThirtyDays_Then_TotalsAndDailyCountsMatch()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var stats = await GetStatsAsync("?days=30");

        // Assert
        stats.ListingId.Should().Be(_listing.Id);
        stats.Days.Should().Be(30);
        stats.Daily.Should().HaveCount(30);
        stats.To.Should().Be(_today);
        stats.From.Should().Be(_today.AddDays(-29));
        stats.Daily.Select(d => d.Date).Should().BeInAscendingOrder();
        stats.Daily[^1].Date.Should().Be(_today);
        stats.Totals.Should().Be(new ListingStatsCounts(Views: 4, Favorites: 2, Messages: 4, Offers: 2));

        Day(stats, _today).Should().Be(new ListingStatsDay(_today, 2, 1, 1, 1));
        Day(stats, _today.AddDays(-1)).Should().Be(new ListingStatsDay(_today.AddDays(-1), 0, 0, 2, 0));
        Day(stats, _today.AddDays(-2)).Should().Be(new ListingStatsDay(_today.AddDays(-2), 1, 0, 0, 1));
        Day(stats, _today.AddDays(-3)).Should().Be(new ListingStatsDay(_today.AddDays(-3), 0, 1, 0, 0));
        Day(stats, _today.AddDays(-5)).Should().Be(new ListingStatsDay(_today.AddDays(-5), 1, 0, 0, 0));
        Day(stats, _today.AddDays(-10)).Should().Be(new ListingStatsDay(_today.AddDays(-10), 0, 0, 1, 0));
        Day(stats, _today.AddDays(-4)).Should().Be(new ListingStatsDay(_today.AddDays(-4), 0, 0, 0, 0));
        stats.Totals.Views.Should().Be(stats.Daily.Sum(d => d.Views));
    }

    [Fact]
    public async Task Given_ActivityOnSeveralDays_When_SellerReadsSevenDays_Then_OlderDataIsLeftOut()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var stats = await GetStatsAsync("?days=7");

        // Assert
        stats.Daily.Should().HaveCount(7);
        stats.Totals.Should().Be(new ListingStatsCounts(Views: 4, Favorites: 2, Messages: 3, Offers: 2));
    }

    [Fact]
    public async Task Given_ActivityOnSeveralDays_When_SellerReadsOneDay_Then_OnlyTodayIsCounted()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var stats = await GetStatsAsync("?days=1");

        // Assert
        stats.Daily.Should().ContainSingle().Which.Date.Should().Be(_today);
        stats.Totals.Should().Be(new ListingStatsCounts(Views: 2, Favorites: 1, Messages: 1, Offers: 1));
    }

    [Fact]
    public async Task Given_NoDaysParameter_When_SellerReads_Then_DefaultsToThirtyDays()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var stats = await GetStatsAsync();

        // Assert
        stats.Days.Should().Be(30);
        stats.Daily.Should().HaveCount(30);
    }

    [Fact]
    public async Task Given_ListingWithoutActivity_When_SellerReads_Then_EveryDayIsZero()
    {
        // Arrange
        await SeedAsync(_seller, _listing);
        AuthenticateAs(_seller.Id);

        // Act
        var stats = await GetStatsAsync("?days=365");

        // Assert
        stats.Daily.Should().HaveCount(365);
        stats.Daily.Should().OnlyContain(d => d.Views == 0 && d.Favorites == 0 && d.Messages == 0 && d.Offers == 0);
        stats.Totals.Should().Be(new ListingStatsCounts(0, 0, 0, 0));
    }

    [Fact]
    public async Task Given_MessagesOfBothSides_When_SellerReads_Then_AllOfThemCountAndCounterOfferIsAnOffer()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_seller.Id);

        // Act
        var stats = await GetStatsAsync("?days=2");

        // Assert: yesterday has the first message (buyer) and the answer (seller); today the buyer's second message and the counter-offer.
        Day(stats, _today.AddDays(-1)).Messages.Should().Be(2);
        Day(stats, _today).Messages.Should().Be(1);
        Day(stats, _today).Offers.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnAdminWhoIsNotTheSeller_When_Reading_Then_Returns200()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_admin.Id, "Admin");

        // Act
        var stats = await GetStatsAsync("?days=30");

        // Assert
        stats.Totals.Views.Should().Be(4);
    }

    [Fact]
    public async Task Given_AMemberWhoIsNotTheSeller_When_Reading_Then_Returns403NotOwner()
    {
        // Arrange
        await SeedActivityAsync();
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{_listing.Id}/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task Given_DaysOutOfRange_When_Reading_Then_Returns400InvalidDays(int days)
    {
        // Arrange
        await SeedAsync(_seller, _listing);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{_listing.Id}/stats?days={days}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Stats.InvalidDays");
        error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Given_NoToken_When_Reading_Then_Returns401()
    {
        // Arrange
        await SeedAsync(_seller, _listing);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{_listing.Id}/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Reading_Then_Returns404ListingNotFound()
    {
        // Arrange
        await SeedAsync(_seller);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
    }
}
