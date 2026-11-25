using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;

namespace CampusMarket.Api.Features.Photos.ReorderPhotos;

// Only the seller changes the order. The ids must be exactly the photos of the listing: no missing
// photo, no unknown or repeated id, so a stale list from another tab is refused, never half applied.
public sealed class ReorderPhotosHandler(
    IListingRepository listings,
    IPhotoRepository photos,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderPhotosCommand, GetListingPhotosResponse>
{
    public async Task<Result<GetListingPhotosResponse>> HandleAsync(ReorderPhotosCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        // A draft of someone else is "not found", as on GET /listings/{id}.
        if (listing is null || !listing.IsVisibleTo(command.RequesterId, command.IsAdmin))
            return Result<GetListingPhotosResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        if (listing.SellerId != command.RequesterId)
            return Result<GetListingPhotosResponse>.Failure(
                Error.Forbidden("Photo.NotSeller", "Only the seller can change the photos of this listing."));

        var current = await photos.GetByListingAsync(command.ListingId, ct);
        var ids = command.PhotoIds;
        if (ids.Count != current.Count || ids.Distinct().Count() != ids.Count || current.Any(p => !ids.Contains(p.Id)))
            return Result<GetListingPhotosResponse>.Failure(
                Error.Validation("Photo.OrderMismatch", "The list must contain every photo of the listing exactly once."));

        var byId = current.ToDictionary(p => p.Id);
        for (var position = 0; position < ids.Count; position++)
        {
            var photo = byId[ids[position]];
            photo.MoveTo(position);
            photos.Update(photo);
        }

        await unitOfWork.CommitAsync(ct);

        var items = current.OrderBy(p => p.Position).Select(PhotoResponse.From).ToList();
        return new GetListingPhotosResponse(items, items.Count);
    }
}
