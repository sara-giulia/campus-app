using CampusMarket.Api.Infrastructure.Search;

namespace CampusMarket.UnitTests.Infrastructure.Search;

public class ListingEmbeddingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ListingId = new("11111111-1111-1111-1111-111111111111");
    private static readonly float[] FirstVector = [0.6f, 0.8f, 0f];
    private static readonly float[] SecondVector = [0f, 0.6f, 0.8f];

    [Fact]
    public void Given_Values_When_Creating_Then_SetsEveryField()
    {
        var embedding = ListingEmbedding.Create(ListingId, FirstVector, "gemma", "hash-1", Now);

        embedding.ListingId.Should().Be(ListingId);
        embedding.Embedding.ToArray().Should().Equal(0.6f, 0.8f, 0f);
        embedding.Model.Should().Be("gemma");
        embedding.TextHash.Should().Be("hash-1");
        embedding.IndexedAt.Should().Be(Now);
    }

    [Fact]
    public void Given_Embedding_When_Replacing_Then_SetsTheNewValuesAndKeepsTheListingId()
    {
        var embedding = ListingEmbedding.Create(ListingId, FirstVector, "gemma", "hash-1", Now);

        embedding.Replace(SecondVector, "gemma-2", "hash-2", Now.AddHours(1));

        embedding.ListingId.Should().Be(ListingId);
        embedding.Embedding.ToArray().Should().Equal(0f, 0.6f, 0.8f);
        embedding.Model.Should().Be("gemma-2");
        embedding.TextHash.Should().Be("hash-2");
        embedding.IndexedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Given_TitleAndDescription_When_Hashing_Then_ReturnsLowerCaseHexSha256OfTitleNewLineDescription()
    {
        var hash = ListingEmbedding.HashOf("Laptop Dell", "Aproape nou");

        hash.Should().Be("a891f4c11fe65b2b0e7565d13c768c78470bf930bdbf4e2941a35b7729a7e57c");
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Given_SameText_When_HashingTwice_Then_ReturnsTheSameHash()
    {
        var first = ListingEmbedding.HashOf("Laptop Dell", "Aproape nou");

        var second = ListingEmbedding.HashOf("Laptop Dell", "Aproape nou");

        second.Should().Be(first);
    }

    [Fact]
    public void Given_ChangedTitle_When_Hashing_Then_ReturnsAnotherHash()
    {
        var original = ListingEmbedding.HashOf("Laptop Dell", "Aproape nou");

        var changed = ListingEmbedding.HashOf("Laptop HP", "Aproape nou");

        changed.Should().NotBe(original);
    }

    [Fact]
    public void Given_ChangedDescription_When_Hashing_Then_ReturnsAnotherHash()
    {
        var original = ListingEmbedding.HashOf("Laptop Dell", "Aproape nou");

        var changed = ListingEmbedding.HashOf("Laptop Dell", "Sigilat");

        changed.Should().NotBe(original);
    }

    [Fact]
    public void Given_NullDescription_When_Hashing_Then_ReturnsTheSameHashAsEmptyDescription()
    {
        var withNull = ListingEmbedding.HashOf("Laptop Dell", null);

        var withEmpty = ListingEmbedding.HashOf("Laptop Dell", "");

        withNull.Should().Be(withEmpty);
        withNull.Should().Be("3fec1fce3068dc3b2c6f43cc859e4463d7a16de1d1567a4d0974327e6f0c3de6");
    }
}
