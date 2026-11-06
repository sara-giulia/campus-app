using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.ReportListing;

public sealed class ReportListingHandler(
    IListingRepository listings,
    IMemberRepository members,
    IReportRepository reports,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ReportListingCommand, ReportListingResponse>
{
    public async Task<Result<ReportListingResponse>> HandleAsync(
        ReportListingCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<ReportListingResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var member = await members.GetByIdAsync(command.ReporterId, ct);

        if (member is null)
            return Result<ReportListingResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.ReporterId}' was not found."));

        var created = Report.Create(command.ReporterId, listing, command.Reason, timeProvider.GetUtcNow());

        if (created.IsFailure)
            return Result<ReportListingResponse>.Failure(created.Error);

        if (await reports.ExistsAsync(command.ReporterId, command.ListingId, ct))
            return Result<ReportListingResponse>.Failure(
                Error.Conflict("Report.AlreadyExists", "You have already reported this listing."));

        var report = created.Value;

        var added = await reports.AddAsync(report, ct);
        if (!added)
            return Result<ReportListingResponse>.Failure(
                Error.Conflict("Report.Duplicate", $"A report with id '{report.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return ReportListingResponse.From(report);
    }
}
