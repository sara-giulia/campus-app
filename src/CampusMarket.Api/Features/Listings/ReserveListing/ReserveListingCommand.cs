using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.ReserveListing;

public sealed record ReserveListingCommand(Guid ListingId, Guid BuyerId) : ICommand<ReserveListingResponse>;
