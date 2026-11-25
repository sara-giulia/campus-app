using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Infrastructure;

// AddDbContext with an options lambda registers both DbContextOptions<T> and IDbContextOptionsConfiguration<T>: remove both.
public sealed class CampusMarketApiFactory : WebApplicationFactory<Program>
{
    // Each factory has its own database on the shared PostgreSQL container; the app creates it with Migrate() at startup.
    // Copies made with WithWebHostBuilder run ConfigureWebHost of this instance, so they use the same database.
    private readonly string _database = $"campusmarket_test_{Guid.NewGuid():N}";

    // Uploaded photos go to a temp folder of this factory, never into the source tree.
    public string PhotosPath { get; } = Path.Combine(Path.GetTempPath(), $"campusmarket-photos-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:PhotosPath", PhotosPath);
        builder.UseSetting("Seed:AdminEmail", "");
        builder.UseSetting("Jwt:Key", TestTokens.Key);
        // The Production check needs a connection string; the DbContext itself is re-registered below on the same database.
        builder.UseSetting("ConnectionStrings:CampusMarket", SharedPostgres.ConnectionStringFor(_database));
        // The reservation expiry background service must not run during tests.
        builder.UseSetting("ReservationExpiry:Enabled", "false");
        // The image generation background service must not run during tests either; GeneratedImagesTests turns it on.
        builder.UseSetting("ImageGeneration:Enabled", "false");
        // Tests register many members; the real limit is tested on purpose in RegisterRateLimitTests.
        builder.UseSetting("RateLimiting:RegisterPermitLimit", "1000");
        builder.ConfigureTestServices(services =>
        {
            // Remove the app's PostgreSQL registration, then point the DbContext at this factory's own database.
            services.RemoveAll<DbContextOptions<CampusMarketDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CampusMarketDbContext>>();
            services.AddDbContext<CampusMarketDbContext>(options =>
                options.UseNpgsql(SharedPostgres.ConnectionStringFor(_database)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        SharedPostgres.DropDatabase(_database);

        if (Directory.Exists(PhotosPath))
            Directory.Delete(PhotosPath, recursive: true);
    }
}
