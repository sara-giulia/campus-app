using CampusMarket.Api.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Title).IsRequired().HasMaxLength(Listing.MaxTitleLength);
        builder.Property(l => l.Description).HasMaxLength(Listing.MaxDescriptionLength);
        
        builder.Property(l => l.Category).HasConversion<string>();
        builder.Property(l => l.Condition).HasConversion<string>();
        builder.Property(l => l.Status).HasConversion<string>();

        builder.Property(l => l.Price)
            .HasConversion(money => money.Amount, amount => Money.Create(amount).Value);

        builder.Property(l => l.Version).IsConcurrencyToken();

        builder.HasMany(l => l.Reservations)
            .WithOne()
            .HasForeignKey(r => r.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(l => l.Reservations)
            .HasField("reservations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
