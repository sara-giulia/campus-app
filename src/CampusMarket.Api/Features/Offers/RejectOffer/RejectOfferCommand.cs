using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Offers.RejectOffer;

// The member comes from the token.
public sealed record RejectOfferCommand(Guid MemberId, Guid OfferId) : ICommand<OfferResponse>;
