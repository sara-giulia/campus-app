using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Offers.MakeOffer;

// The buyer comes from the token.
public sealed record MakeOfferCommand(Guid BuyerId, Guid ListingId, decimal Amount) : ICommand<MakeOfferResponse>;

// The new offer and the conversation it belongs to (started by the offer when there was none).
public sealed record MakeOfferResponse(Guid ConversationId, OfferResponse Offer);
