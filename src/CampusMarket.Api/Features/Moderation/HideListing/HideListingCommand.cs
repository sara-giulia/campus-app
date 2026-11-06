using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.HideListing;

// Only Admins reach this command (the endpoint needs the "Admin" policy).
public sealed record HideListingCommand(Guid ListingId) : ICommand<Guid>;
