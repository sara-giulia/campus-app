using CampusMarket.Api.Common.Search;

namespace CampusMarket.IntegrationTests.Infrastructure;

// An ITextEmbedder without a model: texts that share a concept group get the same direction, so the tests know the similarities.
public sealed class FakeTextEmbedder : ITextEmbedder
{
    public const string Name = "fake-embedder-v1";

    // Group i sets index i of the vector; a text with no group gets one index of its own from 100 up.
    private static readonly string[][] Groups =
    [
        ["laptop", "calculator portabil", "notebook"],
        ["casti", "headphones"],
        ["bicicleta", "bike"],
        ["geaca", "jacheta", "jacket"]
    ];

    private int _queryCalls;
    private int _listingCalls;

    public string ModelName => Name;

    public int QueryCalls => Volatile.Read(ref _queryCalls);

    public int ListingCalls => Volatile.Read(ref _listingCalls);

    public bool ThrowOnQuery { get; set; }

    public bool ThrowOnListing { get; set; }

    // Called with the title at the start of EmbedListing, so a test can change the database while the indexer works.
    public Action<string>? BeforeListing { get; set; }

    public float[] EmbedQuery(string query)
    {
        Interlocked.Increment(ref _queryCalls);
        if (ThrowOnQuery)
            throw new InvalidOperationException("Query embedding failed on purpose.");

        return Embed(query);
    }

    public float[] EmbedListing(string title, string? description)
    {
        BeforeListing?.Invoke(title);
        Interlocked.Increment(ref _listingCalls);
        if (ThrowOnListing)
            throw new InvalidOperationException("Listing embedding failed on purpose.");

        return Embed(string.IsNullOrWhiteSpace(description) ? title : $"{title} {description}");
    }

    private static float[] Embed(string text)
    {
        var folded = SearchText.Fold(text);
        var vector = new float[TextEmbedding.Dimensions];
        for (var i = 0; i < Groups.Length; i++)
        {
            if (Groups[i].Any(word => folded.Contains(word, StringComparison.Ordinal)))
                vector[i] = 1;
        }

        if (vector.All(v => v == 0))
            vector[100 + folded.Sum(c => c) % 600] = 1;

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;
        return vector;
    }
}
