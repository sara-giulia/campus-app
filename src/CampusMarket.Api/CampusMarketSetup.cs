using System.Text.Json.Serialization;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Members;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api;

/// <summary>
/// The whole app setup in one place. Program.cs uses it to run the API,
/// and the API tests use it to start the same API on a free port.
/// </summary>
public static class CampusMarketSetup
{
    public static IServiceCollection AddCampusMarket(this IServiceCollection services)
    {
        // Application clock: tests can replace it with a fake clock
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<CampusMarketDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("CampusMarket")));

        services.AddScoped<IListingRepository, EfListingRepository>();
        services.AddScoped<IMemberRepository, EfMemberRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddHandlers(typeof(CampusMarketSetup).Assembly);

        // Enums are sent as text in JSON ("Active"), not as numbers (1)
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }

    public static WebApplication EnsureCampusMarketDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.Database.EnsureCreated();
        return app;
    }

    public static IEndpointRouteBuilder MapCampusMarket(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => "CampusMarket API is running");

        // All endpoints live under /api/v1; each group is created in its own *Endpoints class
        var api = app.MapGroup("/api/v1");
        api.MapListingsEndpoints();
        api.MapMembersEndpoints();

        return app;
    }
}
