using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Infrastructure.Background;
using CampusMarket.Api.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Infrastructure.Search;

// The only unit tests that write to disk: empty model files in a new folder under the temp path,
// deleted in Dispose. The embedder is never resolved, so the empty files are never loaded.
public sealed class SearchServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _modelFolder = Path.Combine(Path.GetTempPath(), $"campusmarket-search-{Guid.NewGuid():N}");

    public SearchServiceCollectionExtensionsTests() => Directory.CreateDirectory(_modelFolder);

    public void Dispose() => Directory.Delete(_modelFolder, recursive: true);

    private void CreateEmptyFiles(params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(Path.Combine(_modelFolder, name), []);
    }

    private ServiceCollection AddSearch(bool enabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SemanticSearch:Enabled"] = enabled ? "true" : "false",
                ["SemanticSearch:ModelPath"] = _modelFolder
            })
            .Build();
        var services = new ServiceCollection();
        services.AddCampusMarketSearch(configuration);
        return services;
    }

    [Fact]
    public void Given_EnabledAndAllModelFiles_When_Adding_Then_RegistersGemmaTextEmbedderAsSingleton()
    {
        CreateEmptyFiles(GemmaTextEmbedder.ModelFile, GemmaTextEmbedder.WeightsFile, GemmaTextEmbedder.TokenizerFile);

        var services = AddSearch(enabled: true);

        var embedder = services.Should().ContainSingle(d => d.ServiceType == typeof(ITextEmbedder)).Subject;
        embedder.Lifetime.Should().Be(ServiceLifetime.Singleton);
        embedder.ImplementationType.Should().Be<GemmaTextEmbedder>();
    }

    [Fact]
    public void Given_EnabledAndOneModelFileMissing_When_Adding_Then_RegistersNoEmbedder()
    {
        CreateEmptyFiles(GemmaTextEmbedder.ModelFile, GemmaTextEmbedder.WeightsFile);

        var services = AddSearch(enabled: true);

        services.Should().NotContain(d => d.ServiceType == typeof(ITextEmbedder));
    }

    [Fact]
    public void Given_DisabledAndAllModelFiles_When_Adding_Then_RegistersNoEmbedder()
    {
        CreateEmptyFiles(GemmaTextEmbedder.ModelFile, GemmaTextEmbedder.WeightsFile, GemmaTextEmbedder.TokenizerFile);

        var services = AddSearch(enabled: false);

        services.Should().NotContain(d => d.ServiceType == typeof(ITextEmbedder));
    }

    [Fact]
    public async Task Given_NoEmbedder_When_ResolvingSemanticSearch_Then_ReturnsNoSemanticSearchThatFindsNothing()
    {
        var services = AddSearch(enabled: false);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var search = scope.ServiceProvider.GetRequiredService<ISemanticSearch>();

        search.Should().BeOfType<NoSemanticSearch>();
        (await search.FindAsync("laptop", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public void Given_DefaultNumbers_When_ResolvingOptions_Then_PassesValidation()
    {
        using var provider = AddSearch(enabled: false).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SemanticSearchOptions>>().Value;

        options.Rules.Should().Be(SemanticSelectionRules.Default);
        options.ModelPath.Should().Be(_modelFolder);
    }

    [Theory]
    [InlineData("MinSimilarity", "0", "SemanticSearch:MinSimilarity must be between 0 and 1.")]
    [InlineData("MinSimilarity", "1", "SemanticSearch:MinSimilarity must be between 0 and 1.")]
    [InlineData("MaxDistanceFromBest", "-0.01", "SemanticSearch:MaxDistanceFromBest must be between 0 and 2.")]
    [InlineData("MaxDistanceFromBest", "2.01", "SemanticSearch:MaxDistanceFromBest must be between 0 and 2.")]
    [InlineData("MaxResults", "0", "SemanticSearch:MaxResults must be between 1 and 100.")]
    [InlineData("MaxResults", "101", "SemanticSearch:MaxResults must be between 1 and 100.")]
    [InlineData("Threads", "0", "SemanticSearch:Threads must be between 1 and 16.")]
    [InlineData("Threads", "17", "SemanticSearch:Threads must be between 1 and 16.")]
    public void Given_NumberOutsideItsRange_When_ResolvingOptions_Then_ThrowsWithTheMessage(string key, string value, string message)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SemanticSearch:Enabled"] = "false",
                [$"SemanticSearch:{key}"] = value
            })
            .Build();
        using var provider = new ServiceCollection().AddCampusMarketSearch(configuration).BuildServiceProvider();

        Action act = () => _ = provider.GetRequiredService<IOptions<SemanticSearchOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().Equal(message);
    }

    [Fact]
    public void Given_Configuration_When_Adding_Then_RegistersTheSignalAndTheCacheAsSingletons()
    {
        var services = AddSearch(enabled: false);

        services.Should().ContainSingle(d => d.ServiceType == typeof(ListingIndexSignal))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
        services.Should().ContainSingle(d => d.ServiceType == typeof(QueryEmbeddingCache))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_Configuration_When_Adding_Then_RegistersListingIndexServiceAsTheOnlyHostedService()
    {
        var services = AddSearch(enabled: false);

        services.Should().ContainSingle(d => d.ServiceType == typeof(IHostedService))
            .Which.ImplementationType.Should().Be<ListingIndexService>();
    }
}
