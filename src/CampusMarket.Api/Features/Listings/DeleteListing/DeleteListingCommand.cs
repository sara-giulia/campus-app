using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public sealed record DeleteListingCommand(Guid Id) : ICommand<Guid>;
