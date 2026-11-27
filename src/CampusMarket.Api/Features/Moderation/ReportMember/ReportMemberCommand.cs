using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.ReportMember;

// The reporter comes from the token. ConversationId: optional, the conversation between the two where it happened.
public sealed record ReportMemberCommand(Guid ReporterId, Guid ReportedMemberId, string? Reason, Guid? ConversationId)
    : ICommand<ReportMemberResponse>;
