using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.ReserveListing;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.Api.Infrastructure.Search;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class SearchByMeaningTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private const string IndexServiceCategory = "CampusMarket.Api.Infrastructure.Background.ListingIndexService";
    private const string SemanticSearchCategory = "CampusMarket.Api.Infrastructure.Search.SemanticListingSearch";
    private const string CalculatorSearch = "/api/v1/listings?search=calculator%20portabil";

    private static readonly TimeSpan Expected = TimeSpan.FromSeconds(5);

    private readonly CapturingLoggerProvider _logs = new();
    private readonly FakeTextEmbedder _embedder = new();

    // A copy of the API on the same database with the fake embedder (the search by meaning is on) and the capturing logger.
    private WebApplicationFactory<Program> WithSearch() =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            builder.ConfigureTestServices(services => services.AddSingleton<ITextEmbedder>(_embedder));
        });

    // A copy without an embedder, only to read the logs.
    private WebApplicationFactory<Program> WithLogsOnly(params (string Key, string Value)[] settings) =>
        Factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
        });

    private static HttpClient LoggedInClient(WebApplicationFactory<Program> app, Guid memberId)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(memberId));
        return client;
    }

    // Polls every 50 ms until the condition holds or the time runs out; the only place with Task.Delay.
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(50);
        }

        return condition();
    }

    private List<CapturingLoggerProvider.Entry> Logs(string category, LogLevel level) =>
        _logs.Entries.Where(e => e.Category == category && e.Level == level).ToList();

    private List<CapturingLoggerProvider.Entry> IndexedLogs() =>
        Logs(IndexServiceCategory, LogLevel.Information)
            .Where(e => e.Message.StartsWith("Indexed ", StringComparison.Ordinal)).ToList();

    // Waits until the service has logged "Indexed … listings" at least this many times (one line per pass that saved vectors).
    private async Task WaitForIndexedAsync(int passes) =>
        (await WaitUntilAsync(() => IndexedLogs().Count >= passes, Expected))
            .Should().BeTrue($"the service must log 'Indexed … listings' {passes} time(s) within 5 seconds");

    private async Task WaitForLogAsync(string category, LogLevel level, string message) =>
        (await WaitUntilAsync(() => Logs(category, level).Any(e => e.Message.Contains(message, StringComparison.Ordinal)), Expected))
            .Should().BeTrue($"'{message}' must be logged by {category} within 5 seconds");

    private Task<List<ListingEmbedding>> EmbeddingsAsync() =>
        QueryDbAsync(db => db.Set<ListingEmbedding>().AsNoTracking().ToListAsync());

    private static Listing Published(string title, Category category, decimal price, DateTimeOffset createdAt, string? description = null)
    {
        var listing = Listing.Create(SellerId, title, category, Condition.Good, price, createdAt, description).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    // The three listings of the search: the HP matches "calculator portabil" by its words, the Dell only by meaning, the umbrella not at all.
    private async Task<(Listing Hp, Listing Dell, Listing Umbrella)> SeedLaptopsAndUmbrellaAsync()
    {
        var hp = Published("Calculator portabil HP", Category.Electronics, 2000m, CreatedAt.AddHours(2));
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt.AddHours(1));
        var umbrella = Published("Umbrela neagra", Category.Other, 30m, CreatedAt);
        await SeedAsync(hp, dell, umbrella);
        return (hp, dell, umbrella);
    }

    private void DeleteListing(Guid listingId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.Set<Listing>().Where(l => l.Id == listingId).ExecuteDeleteAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Given_ListingsInEveryStatus_When_TheServiceStarts_Then_OnlyActiveListingsGetVectors()
    {
        // Arrange
        var first = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt, "16GB RAM");
        var second = Published("Casti wireless", Category.Electronics, 200m, CreatedAt.AddHours(1));
        var draft = Listing.Create(SellerId, "Bike MTB rosie", Category.Other, Condition.Good, 800m, CreatedAt).Value;
        var hidden = Published("Geaca de iarna", Category.Other, 150m, CreatedAt);
        hidden.Hide();
        await SeedAsync(first, second, draft, hidden);
        await using var app = WithSearch();

        // Act
        app.CreateClient();

        // Assert
        await WaitForIndexedAsync(1);
        IndexedLogs().Single().Message.Should().Be("Indexed 2 listings for the search by meaning");

        var rows = await EmbeddingsAsync();
        rows.Select(r => r.ListingId).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        rows.Should().OnlyContain(r => r.Model == FakeTextEmbedder.Name);
        rows.Single(r => r.ListingId == first.Id).TextHash.Should().Be(ListingEmbedding.HashOf(first.Title, first.Description));
        rows.Single(r => r.ListingId == second.Id).TextHash.Should().Be(ListingEmbedding.HashOf(second.Title, second.Description));
        rows.Should().OnlyContain(r => r.Embedding.ToArray().Length == TextEmbedding.Dimensions);
    }

    [Fact]
    public async Task Given_IndexedListings_When_SearchingCalculatorPortabil_Then_ReturnsTextMatchThenMeaningMatch()
    {
        // Arrange
        var (hp, dell, _) = await SeedLaptopsAndUmbrellaAsync();
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        // Act
        var response = await client.GetAsync(CalculatorSearch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(2);
        body.Items.Select(i => i.Id).Should().Equal(hp.Id, dell.Id);
        body.Items.Select(i => i.Match).Should().Equal(SearchMatch.Text, SearchMatch.Meaning);

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(3);
        (await EmbeddingsAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_CachedEmptySearch_When_DraftIsPublishedAndIndexed_Then_TheSameSearchFindsItByMeaning()
    {
        // Arrange
        var bike = Listing.Create(SellerId, "Bike MTB rosie", Category.Other, Condition.Good, 800m, CreatedAt).Value;
        await SeedAsync(bike);
        await using var app = WithSearch();
        using var anonymous = app.CreateClient();
        using var seller = LoggedInClient(app, SellerId);

        var before = await anonymous.GetFromJsonAsync<GetListingsResponse>("/api/v1/listings?search=bicicleta", Json);
        before!.Items.Should().BeEmpty();

        var publish = await seller.PostAsync($"/api/v1/listings/{bike.Id}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WaitForIndexedAsync(1);

        // Act
        var response = await anonymous.GetAsync("/api/v1/listings?search=bicicleta");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Should().ContainSingle();
        body.Items[0].Id.Should().Be(bike.Id);
        body.Items[0].Match.Should().Be(SearchMatch.Meaning);

        (await EmbeddingsAsync()).Should().ContainSingle(r => r.ListingId == bike.Id);
    }

    [Fact]
    public async Task Given_IndexedListing_When_SellerEditsTheTitle_Then_ItGetsANewVector()
    {
        // Arrange
        // The headphones are not edited: the pass after the edit skips them, so only one new vector is made.
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        var headphones = Published("Casti wireless", Category.Electronics, 200m, CreatedAt.AddHours(1));
        await SeedAsync(dell, headphones);
        await using var app = WithSearch();
        using var seller = LoggedInClient(app, SellerId);
        await WaitForIndexedAsync(1);
        var before = (await EmbeddingsAsync()).Single(r => r.ListingId == dell.Id);
        var headphonesBefore = (await EmbeddingsAsync()).Single(r => r.ListingId == headphones.Id);
        var callsBefore = _embedder.ListingCalls;
        var request = new UpdateListingRequest("Laptop Lenovo ThinkPad", Category.Electronics, Condition.Good, 1500m);

        // Act
        var response = await seller.PutAsJsonAsync($"/api/v1/listings/{dell.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WaitForIndexedAsync(2);

        IndexedLogs()[1].Message.Should().Be("Indexed 1 listings for the search by meaning");
        var rows = await EmbeddingsAsync();
        rows.Should().HaveCount(2);
        var after = rows.Single(r => r.ListingId == dell.Id);
        after.TextHash.Should().Be(ListingEmbedding.HashOf("Laptop Lenovo ThinkPad", null));
        after.TextHash.Should().NotBe(before.TextHash);
        after.IndexedAt.Should().BeAfter(before.IndexedAt);
        _embedder.ListingCalls.Should().Be(callsBefore + 1);
        rows.Single(r => r.ListingId == headphones.Id).IndexedAt.Should().Be(headphonesBefore.IndexedAt);
    }

    [Theory]
    [InlineData("&category=Textbook")]
    [InlineData("&maxPrice=1000")]
    public async Task Given_MeaningMatchOutsideTheFilters_When_Searching_Then_ItIsNotReturned(string filter)
    {
        // Arrange
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        await SeedAsync(dell);
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        // Act
        var response = await client.GetAsync(CalculatorSearch + filter);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);

        (await EmbeddingsAsync()).Should().ContainSingle(r => r.ListingId == dell.Id);
    }

    [Fact]
    public async Task Given_TextAndMeaningMatches_When_SortingByPriceAscending_Then_BothAreOrderedByPrice()
    {
        // Arrange
        var (hp, dell, _) = await SeedLaptopsAndUmbrellaAsync();
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        // Act
        var response = await client.GetAsync(CalculatorSearch + "&sort=price_asc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(dell.Id, hp.Id);
        body.Items.Select(i => i.Match).Should().Equal(SearchMatch.Meaning, SearchMatch.Text);

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(3);
    }

    [Fact]
    public async Task Given_TextAndMeaningMatches_When_SortingByRelevance_Then_KeepsTheOrderOfTheSearch()
    {
        // Arrange
        var (hp, dell, _) = await SeedLaptopsAndUmbrellaAsync();
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        // Act
        var response = await client.GetAsync(CalculatorSearch + "&sort=relevance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(hp.Id, dell.Id);
        body.Items.Select(i => i.Match).Should().Equal(SearchMatch.Text, SearchMatch.Meaning);

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(3);
    }

    [Fact]
    public async Task Given_UnknownSort_When_Searching_Then_Returns400InvalidSort()
    {
        // Arrange
        await SeedLaptopsAndUmbrellaAsync();

        // Act
        var response = await Client.GetAsync(CalculatorSearch + "&sort=cheapest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listings.InvalidSort");
        error.Type.Should().Be(ErrorType.Validation);
        error.Message.Should().Be("Sort must be relevance, newest, price_asc or price_desc.");

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(3);
    }

    [Fact]
    public async Task Given_NoSearch_When_GettingListings_Then_EveryItemHasNoMatch()
    {
        // Arrange
        await SeedLaptopsAndUmbrellaAsync();
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        // Act
        var response = await client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Should().HaveCount(3);
        body.Items.Should().OnlyContain(i => i.Match == null);
        _embedder.QueryCalls.Should().Be(0);

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(3);
    }

    [Fact]
    public async Task Given_SearchOfTwoCharacters_When_Searching_Then_TheEmbedderIsNotUsed()
    {
        // Arrange
        var (hp, _, _) = await SeedLaptopsAndUmbrellaAsync();
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        // Act
        var response = await client.GetAsync("/api/v1/listings?search=ca");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(hp.Id);
        body.Items[0].Match.Should().Be(SearchMatch.Text);
        _embedder.QueryCalls.Should().Be(0);

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(3);
    }

    [Fact]
    public async Task Given_ReservedListing_When_SearchingByMeaning_Then_ItIsNotFound()
    {
        // Arrange
        var buyer = Member.Create("Ana Pop", "ana@student.ro", CreatedAt).Value;
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        await SeedAsync(buyer, dell);
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);

        AuthenticateAs(buyer.Id);
        var reserve = await Client.PostAsync($"/api/v1/listings/{dell.Id}/reservations", null);
        reserve.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var response = await client.GetAsync(CalculatorSearch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Reserved);
        (await EmbeddingsAsync()).Should().ContainSingle(r => r.ListingId == dell.Id, "a Reserved listing keeps its vector");
    }

    [Fact]
    public async Task Given_ReservationCancelled_When_SearchingByMeaning_Then_TheListingIsFoundWithItsOldVector()
    {
        // Arrange
        var buyer = Member.Create("Ana Pop", "ana@student.ro", CreatedAt).Value;
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        await SeedAsync(buyer, dell);
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);
        var callsBefore = _embedder.ListingCalls;

        AuthenticateAs(buyer.Id);
        var reserve = await Client.PostAsync($"/api/v1/listings/{dell.Id}/reservations", null);
        reserve.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservation = await reserve.Content.ReadFromJsonAsync<ReserveListingResponse>(Json);
        var cancel = await Client.DeleteAsync($"/api/v1/listings/{dell.Id}/reservations/{reservation!.Id}");
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await client.GetAsync(CalculatorSearch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(dell.Id);
        body.Items[0].Match.Should().Be(SearchMatch.Meaning);
        _embedder.ListingCalls.Should().Be(callsBefore, "the vector was kept while the listing was reserved");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Given_VectorOfAnOldModel_When_Searching_Then_ItIsNotUsed()
    {
        // Arrange: the start pass fails, so the old row stays as it was.
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        var oldRow = ListingEmbedding.Create(
            dell.Id, new FakeTextEmbedder().EmbedListing(dell.Title, null), "old-model", ListingEmbedding.HashOf(dell.Title, null), CreatedAt);
        await SeedAsync(dell, oldRow);
        _embedder.ThrowOnListing = true;
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForLogAsync(IndexServiceCategory, LogLevel.Error, "Indexing listings for the search by meaning failed");

        // Act
        var response = await client.GetAsync(CalculatorSearch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Should().BeEmpty();
        _embedder.QueryCalls.Should().Be(1);

        (await EmbeddingsAsync()).Single().Model.Should().Be("old-model");
    }

    [Fact]
    public async Task Given_VectorOfAnOldModel_When_TheServiceStarts_Then_ItIsReplaced()
    {
        // Arrange
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        var oldRow = ListingEmbedding.Create(
            dell.Id, new FakeTextEmbedder().EmbedListing(dell.Title, null), "old-model", ListingEmbedding.HashOf(dell.Title, null), CreatedAt);
        await SeedAsync(dell, oldRow);
        await using var app = WithSearch();

        // Act
        app.CreateClient();

        // Assert
        await WaitForIndexedAsync(1);

        var row = (await EmbeddingsAsync()).Single();
        row.ListingId.Should().Be(dell.Id);
        row.Model.Should().Be(FakeTextEmbedder.Name);
        row.IndexedAt.Should().BeAfter(CreatedAt);
    }

    [Fact]
    public async Task Given_IndexedListing_When_SellerDeletesIt_Then_ItsVectorIsDeleted()
    {
        // Arrange
        var dell = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        await SeedAsync(dell);
        await using var app = WithSearch();
        app.CreateClient();
        await WaitForIndexedAsync(1);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{dell.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        (await EmbeddingsAsync()).Should().BeEmpty();
        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingDeletedWhileIndexing_When_TheServiceStarts_Then_TheOtherListingIsStillIndexed()
    {
        // Arrange
        var deleted = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        var kept = Published("Casti wireless", Category.Electronics, 200m, CreatedAt.AddHours(1));
        await SeedAsync(deleted, kept);
        _embedder.BeforeListing = _ => DeleteListing(deleted.Id);
        await using var app = WithSearch();

        // Act
        app.CreateClient();

        // Assert
        await WaitForIndexedAsync(1);
        IndexedLogs().Single().Message.Should().Be("Indexed 1 listings for the search by meaning");

        (await EmbeddingsAsync()).Select(r => r.ListingId).Should().Equal(kept.Id);
        (await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == deleted.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task Given_EmbedderThatFailed_When_ADraftIsPublishedAfterwards_Then_TheServiceKeepsRunningAndIndexesIt()
    {
        // Arrange
        var active = Published("Laptop Dell Latitude", Category.Electronics, 1500m, CreatedAt);
        var draft = Listing.Create(SellerId, "Bike MTB rosie", Category.Other, Condition.Good, 800m, CreatedAt).Value;
        await SeedAsync(active, draft);
        _embedder.ThrowOnListing = true;
        await using var app = WithSearch();
        using var seller = LoggedInClient(app, SellerId);
        await WaitForLogAsync(IndexServiceCategory, LogLevel.Error, "Indexing listings for the search by meaning failed");
        (await EmbeddingsAsync()).Should().BeEmpty();
        _embedder.ThrowOnListing = false;

        // Act
        var response = await seller.PostAsync($"/api/v1/listings/{draft.Id}/publish", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WaitForIndexedAsync(1);

        Logs(IndexServiceCategory, LogLevel.Error).Single().Exception.Should().BeOfType<InvalidOperationException>();
        (await EmbeddingsAsync()).Select(r => r.ListingId).Should().BeEquivalentTo(new[] { active.Id, draft.Id });
    }

    [Fact]
    public async Task Given_EmbedderThatFailsOnQueries_When_Searching_Then_ReturnsTheTextMatchesOnly()
    {
        // Arrange
        var (hp, _, _) = await SeedLaptopsAndUmbrellaAsync();
        await using var app = WithSearch();
        using var client = app.CreateClient();
        await WaitForIndexedAsync(1);
        _embedder.ThrowOnQuery = true;

        // Act
        var response = await client.GetAsync(CalculatorSearch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(hp.Id);
        body.Items[0].Match.Should().Be(SearchMatch.Text);

        var error = Logs(SemanticSearchCategory, LogLevel.Error).Should().ContainSingle().Subject;
        error.Message.Should().Be("Search by meaning failed; only the words were used");
        error.Exception.Should().BeOfType<InvalidOperationException>();

        (await EmbeddingsAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_NoEmbedder_When_SearchingWithDiacritics_Then_FindsTheListingByItsWords()
    {
        // Arrange
        var headphones = Published("Casti wireless", Category.Electronics, 200m, CreatedAt);
        await SeedAsync(headphones);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings?search={Uri.EscapeDataString("Căști")}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(headphones.Id);
        body.Items[0].Match.Should().Be(SearchMatch.Text);

        (await EmbeddingsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NoEmbedder_When_SearchingByMeaning_Then_FindsNothingAndLogsThatTheSearchIsOff()
    {
        // Arrange
        await SeedAsync(Published("Casti wireless", Category.Electronics, 200m, CreatedAt));
        await using var app = WithLogsOnly();
        using var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?search=headphones");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);

        await WaitForLogAsync(IndexServiceCategory, LogLevel.Warning, "Search by meaning is off (SemanticSearch:Enabled is false)");
        (await EmbeddingsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SearchEnabledButNoModelFiles_When_Searching_Then_UsesTheWordsOnlyAndLogsThatTheFilesAreMissing()
    {
        // Arrange
        var modelPath = Directory.CreateTempSubdirectory("campusmarket-no-model-").FullName;
        var headphones = Published("Casti wireless", Category.Electronics, 200m, CreatedAt);
        await SeedAsync(headphones);
        await using var app = WithLogsOnly(("SemanticSearch:Enabled", "true"), ("SemanticSearch:ModelPath", modelPath));
        using var client = app.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?search=casti");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(headphones.Id);
        body.Items[0].Match.Should().Be(SearchMatch.Text);

        await WaitForLogAsync(IndexServiceCategory, LogLevel.Warning, $"the model files are missing in {modelPath}");
        app.Services.GetService<ITextEmbedder>().Should().BeNull();
        (await EmbeddingsAsync()).Should().BeEmpty();

        Directory.Delete(modelPath);
    }

    [Theory]
    [InlineData("SemanticSearch:MinSimilarity", "0")]
    [InlineData("SemanticSearch:MinSimilarity", "1.5")]
    [InlineData("SemanticSearch:MaxResults", "0")]
    [InlineData("SemanticSearch:Threads", "0")]
    [InlineData("SemanticSearch:MaxDistanceFromBest", "-1")]
    public async Task Given_InvalidSemanticSearchSetting_When_StartingTheApp_Then_ThrowsOptionsValidationException(string key, string value)
    {
        // Arrange
        await using var invalid = WithLogsOnly((key, value));

        // Act
        Action act = () => invalid.CreateClient();

        // Assert
        act.Should().Throw<OptionsValidationException>().WithMessage($"*{key}*");

        (await QueryDbAsync(db => db.Set<Listing>().CountAsync())).Should().Be(0);
    }
}
