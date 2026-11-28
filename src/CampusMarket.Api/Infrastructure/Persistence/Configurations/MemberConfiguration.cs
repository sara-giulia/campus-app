using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);

        builder.Property(m => m.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value);
        
        builder.HasIndex(m => m.Email).IsUnique();

        builder.Property(m => m.PasswordHash);

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MemberRole.Student);

        // Suspension (all NULL for a member who was never suspended).
        builder.Property(m => m.SuspendedAt);
        builder.Property(m => m.SuspendedUntil);
        builder.Property(m => m.SuspensionReason).HasMaxLength(Member.MaxSuspensionReasonLength);
        builder.Property(m => m.SuspendedById);
    }
}
