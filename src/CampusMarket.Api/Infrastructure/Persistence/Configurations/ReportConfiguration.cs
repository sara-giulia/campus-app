using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(Report.MaxReasonLength);

        // A member reports a listing at most once. Automatic reports have no reporter (NULL),
        // and NULLs never clash in a unique index.
        builder.HasIndex(r => new { r.ListingId, r.ReporterId }).IsUnique();

        // When the listing or the reporter is deleted, their reports go too.
        builder.HasOne<Listing>()
            .WithMany()
            .HasForeignKey(r => r.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
