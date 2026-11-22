using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Infrastructure;

// AddDbContext with an options lambda registers both DbContextOptions<T> and IDbContextOptionsConfiguration<T>: remove both.
public sealed class CampusMarketApiFactory : WebApplicationFactory<Program>
{
    // The in-memory database lives as long as this connection stays open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CampusMarketApiFactory() => _connection.Open();

    // Uploaded photos go to a temp folder of this factory, never into the source tree.
    public string PhotosPath { get; } = Path.Combine(Path.GetTempPath(), $"campusmarket-photos-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:PhotosPath", PhotosPath);
        builder.UseSetting("Seed:AdminEmail", "");
        builder.UseSetting("Jwt:Key", TestTokens.Key);
        // The reservation expiry background service must not run during tests.
        builder.UseSetting("ReservationExpiry:Enabled", "false");
        // Tests register many members; the real limit is tested on purpose in RegisterRateLimitTests.
        builder.UseSetting("RateLimiting:RegisterPermitLimit", "1000");
        builder.ConfigureTestServices(services =>
        {
            // Remove the app's SQLite file registration, then use the open in-memory connection.
            services.RemoveAll<DbContextOptions<CampusMarketDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CampusMarketDbContext>>();
            services.AddDbContext<CampusMarketDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _connection.Dispose();

        if (Directory.Exists(PhotosPath))
            Directory.Delete(PhotosPath, recursive: true);
    }
}
