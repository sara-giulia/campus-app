using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.TrainingExamples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class TrainingExampleConfiguration : IEntityTypeConfiguration<TrainingExample>
{
    public void Configure(EntityTypeBuilder<TrainingExample> builder)
    {
        builder.ToTable("TrainingExamples");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(Listing.MaxTitleLength);
        builder.Property(e => e.Description).HasMaxLength(Listing.MaxDescriptionLength);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(TrainingExample.MaxLabelLength);

        // At most one example per listing and kind (a later decision relabels it).
        builder.HasIndex(e => new { e.ListingId, e.Kind }).IsUnique();

        // Newest-first reads on the admin endpoint.
        builder.HasIndex(e => e.CreatedAt);

        // No FK to Listings or Members on purpose: an example is kept for retraining even when the
        // listing or the member is deleted later (like SaleRecords). CreatedBy is only an id.
    }
}
