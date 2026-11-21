using CampusMarket.Api.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class SaleRecordConfiguration : IEntityTypeConfiguration<SaleRecord>
{
    public void Configure(EntityTypeBuilder<SaleRecord> builder)
    {
        builder.ToTable("SaleRecords");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Category).HasConversion<string>();
        builder.Property(s => s.Condition).HasConversion<string>();

        // Newest-first reads on the price-accuracy dashboard.
        builder.HasIndex(s => s.SoldAt);

        // No FK to Listings on purpose: a record is kept for analytics even
        // when the listing is later deleted (seller removes their account or
        // the moderation team hides it).
    }
}
