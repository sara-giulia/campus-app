using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.DismissMemberReports;

// Only Admins reach this command (the endpoint needs the "Admin" policy). AdminId comes from the token.
public sealed record DismissMemberReportsCommand(Guid MemberId, Guid AdminId) : ICommand<Guid>;
