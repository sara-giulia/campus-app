using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Views;

namespace CampusMarket.Api.Features.Views.RecordView;

// Counts one unique view per listing, viewer and day. Not counted: the seller's own views and
// listings that are not public (Draft, Hidden, or the listing of a suspended seller).
public sealed class RecordViewHandler(
    IListingRepository listings, IListingViewRepository views, IMemberRepository members, TimeProvider timeProvider)
    : ICommandHandler<RecordViewCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordViewCommand command, CancellationToken ct)
    {
        if (command.MemberId is null && command.VisitorId is null)
            return Result<bool>.Failure(ListingView.ViewerMissingError());

        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        if (listing is null)
            return Result<bool>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        if (listing.SellerId == command.MemberId || listing.Status is ListingStatus.Draft or ListingStatus.Hidden)
            return false;

        var now = timeProvider.GetUtcNow();
        if (await members.IsSuspendedAsync(listing.SellerId, now, ct))
            return false;

        var view = ListingView.Create(listing.Id, ListingView.KeyOf(command.MemberId, command.VisitorId), now);
        return await views.TryAddAsync(view, ct);
    }
}
