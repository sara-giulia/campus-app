namespace CampusMarket.Api.Domain.Offers;

public enum OfferStatus
{
    // Waiting for an answer from the other side.
    Pending,
    // The other side accepted: the listing is reserved for the buyer at this amount.
    Accepted,
    // The other side refused, or the listing was reserved through another offer.
    Rejected,
    // The other side answered with a counter-offer (a new Pending offer whose ParentOfferId is this one).
    Countered,
    // The buyer took back their own offer.
    Withdrawn
}
