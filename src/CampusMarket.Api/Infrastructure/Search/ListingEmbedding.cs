using System.Security.Cryptography;
using System.Text;
using Pgvector;

namespace CampusMarket.Api.Infrastructure.Search;

// The vector of a listing's title and description, one row per listing (table ListingEmbeddings,
// column vector(768) of pgvector). Not part of the domain: only the search uses it.
// Model and TextHash say what the vector was made from: when the seller changes the text or the
// model changes, the row no longer matches and ListingIndexer makes it again.
public sealed class ListingEmbedding
{
    public Guid ListingId { get; private set; }
    public Vector Embedding { get; private set; } = null!;
    public string Model { get; private set; } = string.Empty;
    public string TextHash { get; private set; } = string.Empty;
    public DateTimeOffset IndexedAt { get; private set; }

    private ListingEmbedding() { }

    public static ListingEmbedding Create(Guid listingId, float[] embedding, string model, string textHash, DateTimeOffset now) =>
        new() { ListingId = listingId, Embedding = new Vector(embedding), Model = model, TextHash = textHash, IndexedAt = now };

    public void Replace(float[] embedding, string model, string textHash, DateTimeOffset now)
    {
        Embedding = new Vector(embedding);
        Model = model;
        TextHash = textHash;
        IndexedAt = now;
    }

    // SHA-256 of the title and the description (hex): the text itself is not copied into this table.
    public static string HashOf(string title, string? description) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{title}\n{description}")));
}
