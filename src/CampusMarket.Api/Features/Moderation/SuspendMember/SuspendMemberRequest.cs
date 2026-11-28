namespace CampusMarket.Api.Features.Moderation.SuspendMember;

// Until: an ISO 8601 moment in the future (for example "2026-12-31T21:59:59Z"); missing or null = permanent.
public sealed record SuspendMemberRequest(string? Reason, DateTimeOffset? Until);
