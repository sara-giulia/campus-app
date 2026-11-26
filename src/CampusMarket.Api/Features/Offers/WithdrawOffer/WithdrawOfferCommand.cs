using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Offers.WithdrawOffer;

// The member comes from the token.
public sealed record WithdrawOfferCommand(Guid MemberId, Guid OfferId) : ICommand<OfferResponse>;
