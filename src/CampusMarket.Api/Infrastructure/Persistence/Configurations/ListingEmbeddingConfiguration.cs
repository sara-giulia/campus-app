using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class ListingEmbeddingConfiguration : IEntityTypeConfiguration<ListingEmbedding>
{
    public void Configure(EntityTypeBuilder<ListingEmbedding> builder)
    {
        builder.ToTable("ListingEmbeddings");

        builder.HasKey(e => e.ListingId);
        builder.Property(e => e.ListingId).ValueGeneratedNever();

        builder.Property(e => e.Embedding).HasColumnType($"vector({TextEmbedding.Dimensions})").IsRequired();
        builder.Property(e => e.Model).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TextHash).HasMaxLength(64).IsRequired();

        // HNSW index for the cosine distance (<=>), the one SemanticListingSearch orders by.
        builder.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // The vector goes with the listing.
        builder.HasOne<Listing>()
            .WithOne()
            .HasForeignKey<ListingEmbedding>(e => e.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
