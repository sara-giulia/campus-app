using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.ExpireListings;

// A system job, not called by a user, so it has no RequesterId.
public sealed record ExpireListingsCommand() : ICommand<int>;
