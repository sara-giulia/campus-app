using CampusMarket.Api.Infrastructure.Search;

namespace CampusMarket.UnitTests.Infrastructure.Search;

public class QueryEmbeddingCacheTests
{
    private static readonly float[] LaptopVector = [1f, 0f];
    private static readonly float[] ChairVector = [0f, 1f];

    [Fact]
    public void Given_QueryWithCaseAndExtraSpaces_When_Embedding_Then_EmbedGetsTheLowerCaseCollapsedKey()
    {
        using var cache = new QueryEmbeddingCache();
        var keys = new List<string>();

        cache.GetOrAdd("  Laptop   Dell ", key => { keys.Add(key); return LaptopVector; });

        keys.Should().Equal("laptop dell");
    }

    [Fact]
    public void Given_NewQuery_When_Embedding_Then_ReturnsTheVectorOfEmbed()
    {
        using var cache = new QueryEmbeddingCache();

        var vector = cache.GetOrAdd("laptop", _ => LaptopVector);

        vector.Should().BeSameAs(LaptopVector);
    }

    [Fact]
    public void Given_SameKeyTwice_When_Embedding_Then_CallsEmbedOnlyOnceAndReturnsTheCachedVector()
    {
        using var cache = new QueryEmbeddingCache();
        var keys = new List<string>();
        cache.GetOrAdd("Laptop  Dell", key => { keys.Add(key); return LaptopVector; });

        var vector = cache.GetOrAdd("laptop dell", key => { keys.Add(key); return ChairVector; });

        vector.Should().BeSameAs(LaptopVector);
        keys.Should().Equal("laptop dell");
    }

    [Fact]
    public void Given_AnotherKey_When_Embedding_Then_CallsEmbedAgain()
    {
        using var cache = new QueryEmbeddingCache();
        var keys = new List<string>();
        cache.GetOrAdd("laptop", key => { keys.Add(key); return LaptopVector; });

        var vector = cache.GetOrAdd("scaun", key => { keys.Add(key); return ChairVector; });

        vector.Should().BeSameAs(ChairVector);
        keys.Should().Equal("laptop", "scaun");
    }
}
