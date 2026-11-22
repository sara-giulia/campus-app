using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class ListingCategoryConfiguration : IEntityTypeConfiguration<ListingCategory>
{
    public void Configure(EntityTypeBuilder<ListingCategory> builder)
    {
        builder.ToTable("Categories");

        // The key is the enum value, stored as text like Listings.Category ("Textbook").
        builder.HasKey(c => c.Key);
        builder.Property(c => c.Key)
            .HasConversion<string>()
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(c => c.DisplayName).IsRequired().HasMaxLength(ListingCategory.MaxDisplayNameLength);
        builder.Property(c => c.Description).HasMaxLength(ListingCategory.MaxDescriptionLength);
        builder.Property(c => c.IsActive);

        // The 5 base categories always exist (the ML models use them), all active.
        // The default display names are the labels the frontend shows today.
        builder.HasData(
            Seed(Category.Textbook, "Manuale"),
            Seed(Category.Notes, "Notițe"),
            Seed(Category.Electronics, "Electronice"),
            Seed(Category.LabEquipment, "Echipament de laborator"),
            Seed(Category.Other, "Altele"));
    }

    private static object Seed(Category key, string displayName) =>
        new { Key = key, DisplayName = displayName, Description = (string?)null, IsActive = true };
}
