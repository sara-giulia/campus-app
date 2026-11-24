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

        // A generated image only stands in until the seller adds a real photo: it does not count towards
        // the limit and it goes away with this upload.
        var current = await photos.GetByListingAsync(command.ListingId, ct);
        var generated = current.Where(p => p.IsGenerated).ToList();
        var created = Photo.Create(
            command.UploaderId, listing, current.Count - generated.Count, command.Content, timeProvider.GetUtcNow());
        if (created.IsFailure)
            return Result<PhotoResponse>.Failure(created.Error);

        var photo = created.Value;

        // The file first: if saving the row fails, an unused file is left, never a row without its file.
        await storage.SaveAsync(listing.Id, photo.FileName, command.Content, ct);
        generated.ForEach(photos.Delete);
        await photos.AddAsync(photo, ct);
        await unitOfWork.CommitAsync(ct);

        // The generated files only after the save, so a failed save never leaves a row without its file.
        generated.ForEach(old => storage.Delete(listing.Id, old.FileName));

        return PhotoResponse.From(photo);
    }
}
