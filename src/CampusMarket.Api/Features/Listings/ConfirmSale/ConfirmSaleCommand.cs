using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.ConfirmSale;

public sealed record ConfirmSaleCommand(
    Guid ListingId,
    Guid ReservationId,
    Guid RequesterId,
    bool IsAdmin) : ICommand<Guid>;
