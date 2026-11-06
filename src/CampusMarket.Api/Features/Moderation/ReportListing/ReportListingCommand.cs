using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.ReportListing;

// The reporter comes from the token.
public sealed record ReportListingCommand(Guid ReporterId, Guid ListingId, string? Reason) : ICommand<ReportListingResponse>;
