using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos.UploadPhoto;

public sealed class UploadPhotoHandler(
    IListingRepository listings,
    IPhotoRepository photos,
    IPhotoProcessor processor,
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
        var uploadError = Photo.CheckUpload(command.UploaderId, listing, current.Count - generated.Count, command.Content);
        if (uploadError is not null)
            return Result<PhotoResponse>.Failure(uploadError);

        // Made smaller, turned upright and without EXIF before anything is saved, so the GPS location of
        // the upload is never stored anywhere.
        var processed = processor.Process(command.Content);
        if (processed.IsFailure)
            return Result<PhotoResponse>.Failure(processed.Error);

        var photo = Photo.Create(
            listing, processed.Value, timeProvider.GetUtcNow(),
            current.Where(p => !p.IsGenerated).Select(p => p.Position).DefaultIfEmpty(-1).Max() + 1);

        // The files first: if saving the row fails, unused files are left, never a row without its files.
        try
        {
            await storage.SaveAsync(listing.Id, photo.FileName, processed.Value.Image, photo.ContentType, ct);
            await storage.SaveAsync(listing.Id, photo.ThumbnailFileName!, processed.Value.Thumbnail, photo.ContentType, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<PhotoResponse>.Failure(
                Error.Validation("Photo.StorageFailed", "The photo could not be saved. Try again later."));
        }

        generated.ForEach(photos.Delete);
        await photos.AddAsync(photo, ct);
        await unitOfWork.CommitAsync(ct);

        // The generated files only after the save, so a failed save never leaves a row without its file. Not
        // cancelled any more: the photo is already saved.
        foreach (var old in generated)
            await storage.DeleteAsync(listing.Id, old.FileName, CancellationToken.None);

        return PhotoResponse.From(photo);
    }
}
