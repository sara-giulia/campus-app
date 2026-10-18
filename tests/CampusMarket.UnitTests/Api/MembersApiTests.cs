using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.CreateMember;
using CampusMarket.Api.Features.Members.GetMemberById;
using CampusMarket.Api.Features.Members.GetMembers;
using CampusMarket.Api.Features.Members.UpdateMember;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampusMarket.UnitTests.Api;

public class MembersApiTests
{
    private const string Url = "/api/v1/members";

    private static async Task<CreateMemberResponse> CreateMemberAsync(
        HttpClient client, string name = "Ana", string email = "ana@gmail.com")
    {
        var response = await client.PostAsJsonAsync(Url, new CreateMemberRequest(name, email), TestApi.Json);
        return (await response.Content.ReadFromJsonAsync<CreateMemberResponse>(TestApi.Json))!;
    }

    // ---------- POST /api/v1/members ----------

    [Fact]
    public async Task Create_ValidMember_Returns201WithLocation()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.PostAsJsonAsync(
            Url, new CreateMemberRequest("Ana", "Ana@Gmail.com"), TestApi.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateMemberResponse>(TestApi.Json);
        Assert.NotNull(body);
        Assert.Equal("Ana", body.Name);
        Assert.Equal("ana@gmail.com", body.Email);
        Assert.EndsWith($"{Url}/{body.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_InvalidEmail_Returns400()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.PostAsJsonAsync(
            Url, new CreateMemberRequest("Ana", "not-an-email"), TestApi.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Email.Invalid", error!.Code);
    }

    [Fact]
    public async Task Create_RepositoryRejectsDuplicate_Returns409()
    {
        // A fake repository that always says "this id already exists".
        await using var api = await TestApi.StartAsync(services =>
            services.AddSingleton<IMemberRepository, DuplicateMemberRepository>());

        var response = await api.Client.PostAsJsonAsync(
            Url, new CreateMemberRequest("Ana", "ana@gmail.com"), TestApi.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Member.Duplicate", error!.Code);
    }

    // ---------- GET /api/v1/members/{id} ----------

    [Fact]
    public async Task GetById_ExistingMember_Returns200()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateMemberAsync(api.Client);

        var response = await api.Client.GetAsync($"{Url}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetMemberByIdResponse>(TestApi.Json);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal("ana@gmail.com", body.Email);
        Assert.Equal(created.CreatedAt, body.CreatedAt);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Member.NotFound", error!.Code);
    }

    // ---------- GET /api/v1/members ----------

    [Fact]
    public async Task GetAll_ReturnsEveryMember()
    {
        await using var api = await TestApi.StartAsync();
        await CreateMemberAsync(api.Client, "Ana", "ana@gmail.com");
        await CreateMemberAsync(api.Client, "Maria", "maria@gmail.com");

        var response = await api.Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetMembersResponse>(TestApi.Json);
        Assert.Equal(2, body!.Count);
        Assert.Contains(body.Items, item => item.Email == "maria@gmail.com");
    }

    // ---------- PUT /api/v1/members/{id} ----------

    [Fact]
    public async Task Update_ValidData_Returns204AndSavesTheChanges()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateMemberAsync(api.Client);

        var response = await api.Client.PutAsJsonAsync(
            $"{Url}/{created.Id}", new UpdateMemberRequest("Maria", "maria@gmail.com"), TestApi.Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await api.Client.GetFromJsonAsync<GetMemberByIdResponse>($"{Url}/{created.Id}", TestApi.Json);
        Assert.Equal("Maria", saved!.Name);
        Assert.Equal("maria@gmail.com", saved.Email);
    }

    [Fact]
    public async Task Update_InvalidName_Returns400()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateMemberAsync(api.Client);

        var response = await api.Client.PutAsJsonAsync(
            $"{Url}/{created.Id}", new UpdateMemberRequest("A", "ana@gmail.com"), TestApi.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Error>(TestApi.Json);
        Assert.Equal("Member.InvalidName", error!.Code);
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        await using var api = await TestApi.StartAsync();

        var response = await api.Client.PutAsJsonAsync(
            $"{Url}/{Guid.NewGuid()}", new UpdateMemberRequest("Ana", "ana@gmail.com"), TestApi.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- DELETE /api/v1/members/{id} ----------

    [Fact]
    public async Task Delete_ExistingMember_Returns204AndRemovesIt()
    {
        await using var api = await TestApi.StartAsync();
        var created = await CreateMemberAsync(api.Client);

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
    /// A real duplicate cannot happen through the API, because every member gets a new Guid.
    /// </summary>
    private sealed class DuplicateMemberRepository : IMemberRepository
    {
        public Task<bool> AddAsync(Member member, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Member?>(null);
        public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Member>>([]);
        public void Update(Member member) { /* not used by the 409 test */ }
        public void Delete(Member member) { /* not used by the 409 test */ }
    }
}

