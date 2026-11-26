using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Common.Pricing;
using CampusMarket.Api.Common.Similarity;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

public static class MlServiceCollectionExtensions
{
    public static IServiceCollection AddCampusMarketMl(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MlOptions>().Bind(configuration.GetSection(MlOptions.SectionName));

        // Decision thresholds chosen on validation data when the models are trained; the API refuses to
        // start with a value outside 0–1.
        services.AddOptions<CategorySuggestionOptions>()
            .Bind(configuration.GetSection(CategorySuggestionOptions.SectionName))
            .Validate(o => IsProbability(o.MinConfidence), "Ml:Category:MinConfidence must be between 0 and 1.")
            .ValidateOnStart();
        services.AddOptions<SpamDetectionOptions>()
            .Bind(configuration.GetSection(SpamDetectionOptions.SectionName))
            .Validate(o => IsProbability(o.Threshold), "Ml:Spam:Threshold must be between 0 and 1.")
            .ValidateOnStart();
        services.AddOptions<MessageSpamOptions>()
            .Bind(configuration.GetSection(MessageSpamOptions.SectionName))
            .Validate(o => IsProbability(o.Threshold) && IsProbability(o.BlockThreshold) && o.BlockThreshold >= o.Threshold,
                "Ml:MessageSpam:Threshold and BlockThreshold must be between 0 and 1, and BlockThreshold not below Threshold.")
            .ValidateOnStart();

        // GetValue with the default of MlOptions: no "?? new MlOptions()" side that runs only when the Ml section is missing.
        var modelsFolder = configuration.GetValue(
            $"{MlOptions.SectionName}:{nameof(MlOptions.ModelsPath)}", new MlOptions().ModelsPath)!;
        var modelsPath = Path.Combine(AppContext.BaseDirectory, modelsFolder);

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

        services.AddPredictionEnginePool<SpamInput, SpamPrediction>()
            .FromFile(
                modelName: SpamDetector.ModelName,
                filePath: Path.Combine(modelsPath, "spam-detection.zip"),
                watchForChanges: true);

        services.AddSingleton<ISpamDetector, SpamDetector>();

        services.AddPredictionEnginePool<MessageSpamInput, SpamPrediction>()
            .FromFile(
                modelName: MessageSpamDetector.ModelName,
                filePath: Path.Combine(modelsPath, "message-spam.zip"),
                watchForChanges: true);

        services.AddSingleton<IMessageSpamDetector, MessageSpamDetector>();

        services.AddPredictionEnginePool<SimilarInput, SimilarVector>()
            .FromFile(
                modelName: SimilarListingsFinder.ModelName,
                filePath: Path.Combine(modelsPath, "similar-listings.zip"),
                watchForChanges: true);

        services.AddSingleton<ISimilarListingsFinder, SimilarListingsFinder>();

        return services;
    }

    // One check for every threshold, so the tests of the settings (one too high, one negative)
    // cover both sides of it.
    private static bool IsProbability(double value) => value is >= 0 and <= 1;
}
