namespace CampusMarket.Api.Domain.Notifications;

public enum NotificationType
{
    ReservationCreated,
    ListingSold,
    ReservationExpired,
    ListingExpired,
    // The buyer reviewed a sale; sent to the seller. Stored as text, so no migration is needed.
    ReviewReceived
}
