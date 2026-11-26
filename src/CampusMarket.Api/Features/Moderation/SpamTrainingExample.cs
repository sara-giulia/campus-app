using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.Api.Features.Moderation;

// Shared by HideListing and DismissReports: one spam example per listing, relabelled by a later decision.
// Saved by the caller's CommitAsync, in the same unit of work as the report resolution.
internal static class SpamTrainingExample
{
    public static async Task RecordAsync(
        ITrainingExampleRepository trainingExamples, Listing listing, bool isSpam,
        Guid adminId, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await trainingExamples.GetAsync(listing.Id, TrainingExampleKind.Spam, ct);
        if (existing is not null)
        {
            existing.RelabelSpam(listing, isSpam, adminId, now);
            return;
        }

        var created = TrainingExample.ForSpamDecision(listing, isSpam, adminId, now);
        if (created.IsSuccess)
            trainingExamples.Add(created.Value);
    }
}
