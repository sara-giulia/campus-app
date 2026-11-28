using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.SuspendMember;

// Only Admins reach this command (the endpoint needs the "Admin" policy). AdminId comes from the token.
// Until null = permanent.
public sealed record SuspendMemberCommand(Guid MemberId, Guid AdminId, string? Reason, DateTimeOffset? Until)
    : ICommand<Guid>;
