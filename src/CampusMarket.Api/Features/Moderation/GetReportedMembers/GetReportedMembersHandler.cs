using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.GetReportedMembers;

// The Admins' queue of reported members: only Pending reports (a confirmed or dismissed one was already
// handled), grouped per member, the most reported first, then the most recently reported. After them come
// the members suspended now who have no pending report (a suspension confirms the reports), the most
// recently suspended first, so that an Admin can still lift the suspension.
public sealed class GetReportedMembersHandler(
    IMemberReportRepository reports,
    IMemberRepository members,
    IListingRepository listings,
    TimeProvider timeProvider)
    : IQueryHandler<GetReportedMembersQuery, GetReportedMembersResponse>
{
    public async Task<Result<GetReportedMembersResponse>> HandleAsync(
        GetReportedMembersQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<GetReportedMembersResponse>.Failure(
                Error.Validation("Moderation.InvalidPage", "Page must be at least 1."));
        if (query.PageSize < 1 || query.PageSize > GetReportedMembersQuery.MaxPageSize)
            return Result<GetReportedMembersResponse>.Failure(
                Error.Validation("Moderation.InvalidPageSize",
                    $"Page size must be between 1 and {GetReportedMembersQuery.MaxPageSize}."));

        var pending = await reports.GetAllPendingAsync(ct);
        var membersById = (await members.GetAllAsync(ct)).ToDictionary(m => m.Id);
        var listingsBySeller = (await listings.GetAllAsync(ct)).ToLookup(l => l.SellerId);
        var now = timeProvider.GetUtcNow();

        // Deleting a member deletes the reports on them (cascade), so every report has its member.
        var reported = pending
            .GroupBy(r => r.ReportedMemberId)
            .Where(g => membersById.ContainsKey(g.Key))
            .Select(g => ReportedMemberItem.From(membersById[g.Key], g.ToList(), listingsBySeller[g.Key], now))
            .OrderByDescending(i => i.ReportCount)
            .ThenByDescending(i => i.LastReportedAt)
            .ThenBy(i => i.MemberId);

        var reportedIds = pending.Select(r => r.ReportedMemberId).ToHashSet();
        var suspendedOnly = membersById.Values
            .Where(m => m.IsSuspended(now) && !reportedIds.Contains(m.Id))
            .OrderByDescending(m => m.SuspendedAt)
            .ThenBy(m => m.Id)
            .Select(m => ReportedMemberItem.From(m, [], listingsBySeller[m.Id], now));

        var ordered = reported.Concat(suspendedOnly).ToList();

        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new GetReportedMembersResponse(items, items.Count, ordered.Count, query.Page, query.PageSize);
    }
}
