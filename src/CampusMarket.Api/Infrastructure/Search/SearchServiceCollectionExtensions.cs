using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Infrastructure.Background;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.Api.Infrastructure.Search;

public static class SearchServiceCollectionExtensions
{
    // The search by meaning is on when an ITextEmbedder is registered: the real model when
    // SemanticSearch:Enabled is true and its files are next to the API, or a fake one in the
    // integration tests. Without one the search uses the words only (NoSemanticSearch).
    public static IServiceCollection AddCampusMarketSearch(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SemanticSearchOptions.SectionName);
        services.AddOptions<SemanticSearchOptions>()
            .Bind(section)
            .Validate(o => o.MinSimilarity is > 0 and < 1, "SemanticSearch:MinSimilarity must be between 0 and 1.")
            .Validate(o => o.MaxDistanceFromBest is >= 0 and <= 2, "SemanticSearch:MaxDistanceFromBest must be between 0 and 2.")
            .Validate(o => o.MaxResults is >= 1 and <= 100, "SemanticSearch:MaxResults must be between 1 and 100.")
            .Validate(o => o.Threads is >= 1 and <= 16, "SemanticSearch:Threads must be between 1 and 16.")
            .ValidateOnStart();

        var options = new SemanticSearchOptions();
        section.Bind(options);
        if (options.Enabled && GemmaTextEmbedder.FilesExist(options.FullModelPath))
            services.TryAddSingleton<ITextEmbedder, GemmaTextEmbedder>();

        services.AddSingleton<ListingIndexSignal>();
        services.AddSingleton<QueryEmbeddingCache>();
        // Through a factory, like ISemanticSearch: without an embedder nobody asks for it, and the
        // Development check of every registration at start would fail on a type it cannot build.
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<ListingIndexer>(sp));
        services.AddScoped<ISemanticSearch>(sp => sp.GetService<ITextEmbedder>() is null
            ? new NoSemanticSearch()
            : ActivatorUtilities.CreateInstance<SemanticListingSearch>(sp));
        services.AddHostedService<ListingIndexService>();

        return services;
    }
}
