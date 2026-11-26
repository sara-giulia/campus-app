using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.DismissReports;

// Only Admins reach this command (the endpoint needs the "Admin" policy). AdminId comes from the token.
public sealed record DismissReportsCommand(Guid ListingId, Guid AdminId) : ICommand<Guid>;
