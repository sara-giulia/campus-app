using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Domain.Reports;

// A member says another member should be checked by an Admin (Report is the same idea for a listing).
// ConversationId is optional context: the conversation between the two where it happened.
// The Admin confirms the pending reports by suspending the member, or dismisses them.
// A reporter has at most one Pending report on the same member (checked by the handler, unique index);
// once an Admin resolved it, the member can be reported again.
public sealed class MemberReport
{
    public Guid Id { get; private set; }
    public Guid ReportedMemberId { get; private set; }
    public Guid ReporterId { get; private set; }
    // Stryker disable once String : the factory always sets Reason, so the default value is never observable
    public string Reason { get; private set; } = string.Empty;
    public Guid? ConversationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ReportResolution Resolution { get; private set; } = ReportResolution.Pending;
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedById { get; private set; }

    public bool IsPending => Resolution == ReportResolution.Pending;

    private MemberReport() { }

    // conversation: the one ConversationId points to (loaded by the handler), null when none was given.
    public static Result<MemberReport> Create(
        Guid reporterId, Guid reportedMemberId, string? reason, Conversation? conversation, DateTimeOffset createdAt)
    {
        if (reporterId == Guid.Empty)
            return Result<MemberReport>.Failure(
                Error.Validation("MemberReport.ReporterEmpty", "Reporter id must not be empty."));

        if (reporterId == reportedMemberId)
            return Result<MemberReport>.Failure(
                Error.Validation("MemberReport.Self", "You cannot report yourself."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result<MemberReport>.Failure(
                Error.Validation("MemberReport.ReasonEmpty", "Reason must not be empty."));
        if (reason.Trim().Length > Report.MaxReasonLength)
            return Result<MemberReport>.Failure(
                Error.Validation("MemberReport.ReasonTooLong", $"Reason must have at most {Report.MaxReasonLength} characters."));

        // The conversation must be between the two of them, otherwise it says nothing about this member.
        if (conversation is not null
            && !(conversation.IsParticipant(reporterId) && conversation.IsParticipant(reportedMemberId)))
            return Result<MemberReport>.Failure(ConversationInvalidError());

        return new MemberReport
        {
            Id = Guid.NewGuid(),
            ReportedMemberId = reportedMemberId,
            ReporterId = reporterId,
            Reason = reason.Trim(),
            ConversationId = conversation?.Id,
            CreatedAt = createdAt
        };
    }

    public static Error ConversationInvalidError() =>
        Error.Validation("MemberReport.ConversationInvalid", "The conversation must be between you and the reported member.");

    public Result<MemberReport> Confirm(Guid adminId, DateTimeOffset resolvedAt) =>
        Resolve(ReportResolution.Confirmed, adminId, resolvedAt);

    public Result<MemberReport> Dismiss(Guid adminId, DateTimeOffset resolvedAt) =>
        Resolve(ReportResolution.Dismissed, adminId, resolvedAt);

    private Result<MemberReport> Resolve(ReportResolution resolution, Guid adminId, DateTimeOffset resolvedAt)
    {
        if (!IsPending)
            return Result<MemberReport>.Failure(
                Error.Validation("Report.AlreadyResolved", "The report has already been resolved."));

        Resolution = resolution;
        ResolvedAt = resolvedAt;
        ResolvedById = adminId;
        return this;
    }
}
