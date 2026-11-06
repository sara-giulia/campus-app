using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("Favorites");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        // A member saves a listing at most once.
        builder.HasIndex(f => new { f.MemberId, f.ListingId }).IsUnique();

        // When the listing or the member is deleted, their favorites go too.
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(f => f.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(f => f.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
