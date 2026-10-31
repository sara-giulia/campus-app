using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public sealed record DeleteListingCommand(Guid Id, Guid RequesterId, bool IsAdmin) : ICommand<Guid>;
