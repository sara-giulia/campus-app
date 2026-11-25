using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.RenewListing;

// Only the seller renews a listing (an Admin does not), so the command has no IsAdmin.
public sealed record RenewListingCommand(Guid Id, Guid RequesterId) : ICommand<Guid>;
