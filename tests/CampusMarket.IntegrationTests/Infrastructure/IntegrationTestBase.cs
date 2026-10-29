using System.Text.Json;
using System.Text.Json.Serialization;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    // Same JSON settings as the API: camelCase names, enums as text.
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected CampusMarketApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new CampusMarketApiFactory();
        Client = Factory.CreateClient();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    // Arrange: saves entities straight into the database, without going through another endpoint.
    protected async Task SeedAsync(params object[] entities)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    // Assert: reads the database with a new DbContext, so it sees what is really saved.
    protected async Task<T> QueryDbAsync<T>(Func<CampusMarketDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        return await query(db);
    }
    
    // Logs the test client in as this member, so protected endpoints accept its requests.
    protected void AuthenticateAs(Guid memberId, string role = "Student") =>
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokens.Create(memberId, role));
}
