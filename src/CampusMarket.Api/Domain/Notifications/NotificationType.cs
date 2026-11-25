namespace CampusMarket.Api.Domain.Notifications;

public enum NotificationType
{
    ReservationCreated,
    ListingSold,
    ReservationExpired,
    ListingExpired,
    // The buyer reviewed a sale; sent to the seller. Stored as text, so no migration is needed.
    ReviewReceived,
    // Price offers (Features/Offers), stored as text like the others:
    // a new offer from the buyer, sent to the seller;
    OfferReceived,
    // a counter-offer, sent to the other side;
    OfferCountered,
    // an accepted offer (the listing is reserved at that price), sent to both sides;
    OfferAccepted,
    // a rejected offer, sent to the side that proposed it (or to the buyer when another offer was accepted).
    OfferRejected
}
