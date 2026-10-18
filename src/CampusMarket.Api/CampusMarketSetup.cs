using System.Text.Json.Serialization;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Members;
using CampusMarket.Api.Infrastructure;

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

        services.AddSingleton<IListingRepository, InMemoryListingRepository>();
        services.AddSingleton<IMemberRepository, InMemoryMemberRepository>();

        // Enums are sent as text in JSON ("Active"), not as numbers (1)
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
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
