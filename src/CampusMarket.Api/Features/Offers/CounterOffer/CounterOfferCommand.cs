using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Offers.CounterOffer;

// The member comes from the token. Returns the new Pending offer (the answered one becomes Countered).
public sealed record CounterOfferCommand(Guid MemberId, Guid OfferId, decimal Amount) : ICommand<OfferResponse>;
