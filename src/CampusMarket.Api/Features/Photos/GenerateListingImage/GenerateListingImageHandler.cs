using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos.GenerateListingImage;

// Makes a picture for an Active listing that has no photo and saves it as a generated photo. The checks
// run again here, because the seller may have added a photo, or hidden or sold the listing, while it
// waited in the queue.
public sealed class GenerateListingImageHandler(
    IListingRepository listings,
    IPhotoRepository photos,
    ICategoryRepository categories,
    IImageGenerator generator,
    IPhotoStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<GenerateListingImageCommand, PhotoResponse>
{
    public async Task<Result<PhotoResponse>> HandleAsync(GenerateListingImageCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);
        if (listing is null)
            return Result<PhotoResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        // Checked before drawing, so no image is made for nothing.
        var existing = await photos.CountForListingAsync(listing.Id, ct);
        var listingError = Photo.CheckCanGenerate(listing, existing);
        if (listingError is not null)
            return Result<PhotoResponse>.Failure(listingError);

        // The name an Admin gave the category; the key itself if the row is missing.
        var category = await categories.GetByKeyAsync(listing.Category, ct);
        var prompt = new ImagePrompt(
            listing.Title, listing.Description, listing.Category, category?.DisplayName ?? listing.Category.ToString());
        var image = await generator.GenerateAsync(prompt, ct);

        // Fails only when the generator gave an empty or too large file.
        var created = Photo.CreateGenerated(listing, existing, image.Content, image.Format, timeProvider.GetUtcNow());
        if (created.IsFailure)
            return Result<PhotoResponse>.Failure(created.Error);

        var photo = created.Value;

        // The file first, as for an upload: a failed save leaves an unused file, never a row without its file.
        await storage.SaveAsync(listing.Id, photo.FileName, image.Content, photo.ContentType, ct);
        await photos.AddAsync(photo, ct);
        await unitOfWork.CommitAsync(ct);

        return PhotoResponse.From(photo);
    }
}
