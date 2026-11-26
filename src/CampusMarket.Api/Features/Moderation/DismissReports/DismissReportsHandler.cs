using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.Api.Features.Moderation.DismissReports;

// The Admin found nothing wrong: the pending reports of the listing are dismissed (the listing stays as it is)
// and one "not spam" training example is recorded.
public sealed class DismissReportsHandler(
    IListingRepository listings,
    IReportRepository reports,
    ITrainingExampleRepository trainingExamples,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DismissReportsCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DismissReportsCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var pending = await reports.GetPendingByListingAsync(listing.Id, ct);

        if (pending.Count == 0)
            return Result<Guid>.Failure(
                Error.Validation("Report.NoPendingReports", "The listing has no pending reports."));

        var now = timeProvider.GetUtcNow();
        foreach (var report in pending)
            report.Dismiss(now);

        await SpamTrainingExample.RecordAsync(trainingExamples, listing, isSpam: false, command.AdminId, now, ct);

        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
