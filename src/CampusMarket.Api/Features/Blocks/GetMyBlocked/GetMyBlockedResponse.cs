namespace CampusMarket.Api.Features.Blocks.GetMyBlocked;

public sealed record GetMyBlockedResponse(IReadOnlyList<BlockedMemberItem> Items);

public sealed record BlockedMemberItem(Guid MemberId, string Name, DateTimeOffset BlockedAt);
