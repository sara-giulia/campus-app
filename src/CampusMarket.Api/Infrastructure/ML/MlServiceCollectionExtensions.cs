using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Common.Pricing;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

public static class MlServiceCollectionExtensions
{
    public static IServiceCollection AddCampusMarketMl(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MlOptions>().Bind(configuration.GetSection(MlOptions.SectionName));

        var options = configuration.GetSection(MlOptions.SectionName).Get<MlOptions>() ?? new MlOptions();
        var modelsPath = Path.Combine(AppContext.BaseDirectory, options.ModelsPath);

        // PredictionEnginePool is thread safe and comes from Microsoft.Extensions.ML.
        // The model file path is resolved against AppContext.BaseDirectory, so the API
        // picks up the copy of ml/models/*.zip that the csproj drops next to its binaries.
        services.AddPredictionEnginePool<PriceInput, PricePrediction>()
            .FromFile(
                modelName: PriceSuggester.ModelName,
                filePath: Path.Combine(modelsPath, "price-regression.zip"),
                watchForChanges: true);

        services.AddSingleton<IPriceSuggester, PriceSuggester>();

        services.AddPredictionEnginePool<CategoryInput, CategoryPrediction>()
            .FromFile(
                modelName: CategorySuggester.ModelName,
                filePath: Path.Combine(modelsPath, "category-classification.zip"),
                watchForChanges: true);

        services.AddSingleton<ICategorySuggester, CategorySuggester>();

        // Each future model (spam in B5) registers its own
        // PredictionEnginePool here alongside the ones above.

        return services;
    }
}
