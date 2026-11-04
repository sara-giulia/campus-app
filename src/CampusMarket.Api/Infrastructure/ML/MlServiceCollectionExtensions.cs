namespace CampusMarket.Api.Infrastructure.ML;

public static class MlServiceCollectionExtensions
{
    public static IServiceCollection AddCampusMarketMl(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MlOptions>().Bind(configuration.GetSection(MlOptions.SectionName));

        // Each model registers its own PredictionEnginePool here in B3 (price),
        // B4 (category) and B5 (spam). PredictionEnginePool is thread safe and
        // comes from Microsoft.Extensions.ML.

        return services;
    }
}
