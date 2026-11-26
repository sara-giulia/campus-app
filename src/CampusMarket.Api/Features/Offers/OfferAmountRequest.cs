namespace CampusMarket.Api.Features.Offers;

// Body of POST /listings/{id}/offers and POST /offers/{id}/counter. A missing amount is treated as 0
// and refused with Offer.AmountInvalid.
public sealed record OfferAmountRequest(decimal? Amount);
