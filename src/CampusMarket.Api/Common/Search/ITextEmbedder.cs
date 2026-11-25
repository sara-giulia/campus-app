namespace CampusMarket.Api.Common.Search;

// Turns a text into a vector (an embedding): texts with a close meaning get vectors that point the same
// way, even with different words ("calculator portabil" and "laptop"). The vectors have Dimensions
// numbers and length 1, so the cosine of two of them is just their dot product.
// The implementation runs a pretrained model locally (Infrastructure/Search/GemmaTextEmbedder).
public interface ITextEmbedder
{
    // Saved next to every vector: when the model changes, the listings are indexed again.
    string ModelName { get; }

    float[] EmbedQuery(string query);

    float[] EmbedListing(string title, string? description);
}

public static class TextEmbedding
{
    // The size of the vectors stored in ListingEmbeddings ("vector(768)" in the database).
    public const int Dimensions = 768;
}
