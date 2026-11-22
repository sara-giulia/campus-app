using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos.DeletePhoto;

// The seller removes one photo of their listing (to make room for another one, for example).
public sealed class DeletePhotoHandler(
    IListingRepository listings,
    IPhotoRepository photos,
    IPhotoStorage storage,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePhotoCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(DeletePhotoCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        if (listing.SellerId != command.RequesterId)
            return Result<Guid>.Failure(
                Error.Forbidden("Photo.NotSeller", "Only the seller can change the photos of this listing."));

        // A photo of another listing is "not found" here, so the URL cannot be used to delete it.
        var photo = await photos.GetByIdAsync(command.PhotoId, ct);
        if (photo is null || photo.ListingId != listing.Id)
            return Result<Guid>.Failure(
                Error.NotFound("Photo.NotFound", $"Photo '{command.PhotoId}' was not found."));

        photos.Delete(photo);
        await unitOfWork.CommitAsync(ct);

        // The row first: if removing the file fails, an unused file is left, never a row without its file.
        storage.Delete(listing.Id, photo.FileName);

        return photo.Id;
    }
}
