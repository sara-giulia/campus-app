using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.LiftSuspension;

// Only Admins reach this command (the endpoint needs the "Admin" policy).
public sealed record LiftSuspensionCommand(Guid MemberId) : ICommand<Guid>;
