using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.PublishListing;

public sealed record PublishListingCommand(Guid Id, Guid RequesterId, bool IsAdmin) : ICommand<Guid>;
