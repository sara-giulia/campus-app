using CampusMarket.Api.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampusMarket.Api.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Type).HasConversion<string>();
        builder.Property(n => n.Message).IsRequired().HasMaxLength(Notification.MaxMessageLength);

        // Newest-first reads of a member's notifications.
        builder.HasIndex(n => new { n.MemberId, n.CreatedAt });

        // No FK on MemberId (matches Reservations): a notification is kept for its
        // member even if the referenced listing is gone, and the member row is not
        // required to exist as a stand-alone entity (same shape as Reservation.MemberId).
    }
}
