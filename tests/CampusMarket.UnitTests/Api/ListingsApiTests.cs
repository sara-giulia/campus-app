using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampusMarket.UnitTests.Api;

public class ListingsApiTests
{
    private const string Url = "/api/v1/listings";

    private static CreateListingRequest ValidRequest(string title = "Java Book") =>
        new(Guid.NewGuid(), title, Category.Textbook, Condition.Good, 50);

    private static async Task<CreateListingResponse> CreateListingAsync(HttpClient client, string title = "Java Book")
    {
        var response = await client.PostAsJsonAsync(Url, ValidRequest(title), TestApi.Json);
        return (await response.Content.ReadFromJsonAsync<CreateListingResponse>(TestApi.Json))!;
    }

    [Fact]
    public async Task Root_ShouldSayTheApiIsRunning()
    {
        await using var api = await TestApi.StartAsync();

        var text = await api.Client.GetStringAsync("/");

        Assert.Equal("CampusMarket API is running", text);
    }

    // ---------- POST /api/v1/listings ----------

    [Fact]
    public async Task Create_ValidListing_Returns201WithLocation()
    {
        await using var api = await TestApi.StartAsync();
        var request = ValidRequest();

        var response = await api.Client.PostAsJsonAsync(Url, request, TestApi.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateListingResponse>(TestApi.Json);
        Assert.NotNull(body);
        Assert.Equal(request.SellerId, body.SellerId);
        Assert.Equal("Java Book", body.Title);
        Assert.Equal(50m, body.Price);
        Assert.Equal(ListingStatus.Draft, body.Status);
        Assert.Equal(1, body.Version);
        Assert.EndsWith($"{Url}/{body.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_EmptyTitle_Returns400()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.PostAsJsonAsync(Url, ValidRequest(title: ""), TestApi.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Listing.TitleEmpty", error!.Code);
    }

    [Fact]
    public async Task Create_RepositoryRejectsDuplicate_Returns409()
    {
        // A fake repository that always says "this id already exists".
        await using var api = await TestApi.StartAsync(services =>
            services.AddSingleton<IListingRepository, DuplicateListingRepository>());

        var response = await api.Client.PostAsJsonAsync(Url, ValidRequest(), TestApi.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Listing.Duplicate", error!.Code);
    }

    // ---------- GET /api/v1/listings/{id} ----------

    [Fact]
    public async Task GetById_ExistingListing_Returns200()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateListingAsync(api.Client);

        var response = await api.Client.GetAsync($"{Url}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetListingByIdResponse>(TestApi.Json);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("Java Book", body.Title);
        Assert.Equal(Category.Textbook, body.Category);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Listing.NotFound", error!.Code);
    }

    // ---------- GET /api/v1/listings ----------

    [Fact]
    public async Task GetAll_ReturnsEveryListing()
    {
        await using var api = await TestApi.StartAsync();
        await CreateListingAsync(api.Client, "First");
        await CreateListingAsync(api.Client, "Second");

        var response = await api.Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(TestApi.Json);
        Assert.Equal(2, body!.Count);
        Assert.Equal(2, body.Items.Count);
        Assert.Contains(body.Items, item => item.Title == "First");
    }

    // ---------- PUT /api/v1/listings/{id} ----------

    [Fact]
    public async Task Update_ValidData_Returns204AndSavesTheChanges()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateListingAsync(api.Client);
        var update = new UpdateListingRequest("C# Notes", Category.Notes, Condition.Worn, 20);

        var response = await api.Client.PutAsJsonAsync($"{Url}/{created.Id}", update, TestApi.Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await api.Client.GetFromJsonAsync<GetListingByIdResponse>($"{Url}/{created.Id}", TestApi.Json);
        Assert.Equal("C# Notes", saved!.Title);
        Assert.Equal(20m, saved.Price);
        Assert.Equal(2, saved.Version);
    }

    [Fact]
    public async Task Update_InvalidPrice_Returns400()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateListingAsync(api.Client);
        var update = new UpdateListingRequest("Java Book", Category.Textbook, Condition.Good, 0);

        var response = await api.Client.PutAsJsonAsync($"{Url}/{created.Id}", update, TestApi.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Money.Invalid", error!.Code);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        await using var api = await TestApi.StartAsync();
        var update = new UpdateListingRequest("Java Book", Category.Textbook, Condition.Good, 50);

        var response = await api.Client.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", update, TestApi.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- DELETE /api/v1/listings/{id} ----------

    [Fact]
    public async Task Delete_ExistingListing_Returns204AndRemovesIt()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateListingAsync(api.Client);

        var response = await api.Client.DeleteAsync($"{Url}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var afterDelete = await api.Client.GetAsync($"{Url}/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.DeleteAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Fake repository for the 409 test: AddAsync always answers "already exists".
    /// A real duplicate cannot happen through the API, because every listing gets a new Guid.
    /// </summary>
    private sealed class DuplicateListingRepository : IListingRepository
    {
        public Task<bool> AddAsync(Listing listing, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Listing?>(null);
        public Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Listing>>([]);
        public void Update(Listing listing) { /* not used by the 409 test */ }
        public void Delete(Listing listing) { /* not used by the 409 test */ }
    }
}
