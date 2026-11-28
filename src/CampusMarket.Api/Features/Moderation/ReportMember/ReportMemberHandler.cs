using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.ReportMember;

// A member reports another member to the Admins, with a reason. One Pending report per pair.
public sealed class ReportMemberHandler(
    IMemberRepository members,
    IConversationRepository conversations,
    IMemberReportRepository reports,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ReportMemberCommand, ReportMemberResponse>
{
    public async Task<Result<ReportMemberResponse>> HandleAsync(ReportMemberCommand command, CancellationToken ct)
    {
        if (await members.GetByIdAsync(command.ReportedMemberId, ct) is null)
            return Result<ReportMemberResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.ReportedMemberId}' was not found."));

        if (await members.GetByIdAsync(command.ReporterId, ct) is null)
            return Result<ReportMemberResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.ReporterId}' was not found."));

        // An unknown conversation gets the same answer as someone else's, so ids cannot be probed.
        Conversation? conversation = null;
        if (command.ConversationId is { } conversationId)
        {
            conversation = await conversations.GetByIdAsync(conversationId, ct);
            if (conversation is null)
                return Result<ReportMemberResponse>.Failure(MemberReport.ConversationInvalidError());
        }

        var created = MemberReport.Create(
            command.ReporterId, command.ReportedMemberId, command.Reason, conversation, timeProvider.GetUtcNow());
        if (created.IsFailure)
            return Result<ReportMemberResponse>.Failure(created.Error);

        if (await reports.HasPendingAsync(command.ReporterId, command.ReportedMemberId, ct))
            return Result<ReportMemberResponse>.Failure(
                Error.Conflict("MemberReport.AlreadyExists", "You have already reported this member; an Admin will check it."));

        reports.Add(created.Value);
        await unitOfWork.CommitAsync(ct);

        return ReportMemberResponse.From(created.Value);
    }
}
