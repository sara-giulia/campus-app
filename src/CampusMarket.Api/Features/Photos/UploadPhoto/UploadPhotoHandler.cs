using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos.UploadPhoto;

public sealed class UploadPhotoHandler(
    IListingRepository listings,
    IPhotoRepository photos,
    IPhotoStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<UploadPhotoCommand, PhotoResponse>
{
    public async Task<Result<PhotoResponse>> HandleAsync(UploadPhotoCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        if (listing is null)
            return Result<PhotoResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var existing = await photos.CountForListingAsync(command.ListingId, ct);
        var created = Photo.Create(command.UploaderId, listing, existing, command.Content, timeProvider.GetUtcNow());
        if (created.IsFailure)
            return Result<PhotoResponse>.Failure(created.Error);

        var photo = created.Value;

        // The file first: if saving the row fails, an unused file is left, never a row without its file.
        await storage.SaveAsync(listing.Id, photo.FileName, command.Content, ct);
        await photos.AddAsync(photo, ct);
        await unitOfWork.CommitAsync(ct);

        return PhotoResponse.From(photo);
    }
}
