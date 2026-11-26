using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class MemberBlockConfiguration : IEntityTypeConfiguration<MemberBlock>
{
    public void Configure(EntityTypeBuilder<MemberBlock> builder)
    {
        builder.ToTable("MemberBlocks");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        // A member blocks another member at most once.
        builder.HasIndex(b => new { b.BlockerId, b.BlockedId }).IsUnique();

        // When either member is deleted, the block goes too.
        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(b => b.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Member>()
            .WithMany()
            .HasForeignKey(b => b.BlockedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
