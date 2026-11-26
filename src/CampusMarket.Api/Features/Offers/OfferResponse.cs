using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.Api.Features.Offers;

// One offer, as the offer endpoints and GET /conversations/{id} return it.
public sealed record OfferResponse(
    Guid Id,
    Guid ListingId,
    Guid BuyerId,
    Guid SellerId,
    Guid ProposedById,
    decimal Amount,
    OfferStatus Status,
    Guid? ParentOfferId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt)
{
    public static OfferResponse From(Offer offer) => new(
        offer.Id,
        offer.ListingId,
        offer.BuyerId,
        offer.SellerId,
        offer.ProposedById,
        offer.Amount,
        offer.Status,
        offer.ParentOfferId,
        offer.CreatedAt,
        offer.RespondedAt);
}
