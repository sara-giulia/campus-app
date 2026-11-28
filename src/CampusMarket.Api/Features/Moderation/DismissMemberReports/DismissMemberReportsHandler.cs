using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.DismissMemberReports;

// The Admin found nothing wrong: the pending reports on the member are dismissed; the member is not changed.
public sealed class DismissMemberReportsHandler(
    IMemberRepository members,
    IMemberReportRepository reports,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DismissMemberReportsCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DismissMemberReportsCommand command, CancellationToken ct)
    {
        if (await members.GetByIdAsync(command.MemberId, ct) is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.MemberId}' was not found."));

        var pending = await reports.GetPendingByMemberAsync(command.MemberId, ct);
        if (pending.Count == 0)
            return Result<Guid>.Failure(
                Error.Validation("Report.NoPendingReports", "The member has no pending reports."));

        var now = timeProvider.GetUtcNow();
        foreach (var report in pending)
            report.Dismiss(command.AdminId, now);

        await unitOfWork.CommitAsync(ct);

        return command.MemberId;
    }
}
