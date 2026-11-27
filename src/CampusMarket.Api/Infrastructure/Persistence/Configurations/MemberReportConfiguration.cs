using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class MemberReportConfiguration : IEntityTypeConfiguration<MemberReport>
{
    public void Configure(EntityTypeBuilder<MemberReport> builder)
    {
        builder.ToTable("MemberReports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Reason).IsRequired().HasMaxLength(Report.MaxReasonLength);

        // Stored as text, like the other enums.
        builder.Property(r => r.Resolution)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ReportResolution.Pending);

        // The moderation queue reads the pending reports of a member.
        builder.HasIndex(r => new { r.ReportedMemberId, r.Resolution });

        // At most one Pending report per reporter and reported member; resolved ones do not count,
        // so the member can be reported again after the Admin's decision.
        builder.HasIndex(r => new { r.ReporterId, r.ReportedMemberId })
            .IsUnique()
            .HasFilter("\"Resolution\" = 'Pending'");

        // When either member is deleted, their reports go too (PostgreSQL accepts the two cascade paths).
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(r => r.ReportedMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Cascade);

        // The conversation is only context: when it is deleted the report stays, without it.
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(r => r.ConversationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
