using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("Photos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.FileName).HasMaxLength(64).IsRequired();
        builder.Property(p => p.ContentType).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.ListingId);

        // The rows go with the listing; DeleteListingHandler removes the files.
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(p => p.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
