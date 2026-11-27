namespace CampusMarket.Api.Features.Moderation.ReportMember;

public sealed record ReportMemberRequest(string? Reason, Guid? ConversationId);
