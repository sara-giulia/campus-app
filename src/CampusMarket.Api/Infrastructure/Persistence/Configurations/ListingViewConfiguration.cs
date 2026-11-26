using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class ListingViewConfiguration : IEntityTypeConfiguration<ListingView>
{
    public void Configure(EntityTypeBuilder<ListingView> builder)
    {
        builder.ToTable("ListingViews");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.ViewerKey).IsRequired().HasMaxLength(ListingView.MaxViewerKeyLength);

        // A viewer is counted once per listing and day.
        builder.HasIndex(v => new { v.ListingId, v.ViewerKey, v.Day }).IsUnique();

        // When the listing is deleted, its views go too.
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(v => v.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
