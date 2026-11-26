using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.Api.Features.Moderation.HideListing;

// Hiding a reported listing confirms its pending reports and records one "spam" training example.
// A listing without pending reports is hidden as before, with no example.
public sealed class HideListingHandler(
    IListingRepository repository,
    IReportRepository reports,
    ITrainingExampleRepository trainingExamples,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<HideListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(HideListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var hidden = listing.Hide();

        if (hidden.IsFailure)
            return Result<Guid>.Failure(hidden.Error);

        var now = timeProvider.GetUtcNow();
        var pending = await reports.GetPendingByListingAsync(listing.Id, ct);
        if (pending.Count > 0)
        {
            foreach (var report in pending)
                report.Confirm(now);

            await SpamTrainingExample.RecordAsync(trainingExamples, listing, isSpam: true, command.AdminId, now, ct);
        }

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
