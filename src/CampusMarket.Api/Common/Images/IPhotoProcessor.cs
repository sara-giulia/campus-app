using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Common.Images;

// Turns an uploaded photo (already checked by Photo.CheckUpload) into what is kept: the image turned
// upright, at most MaxImageSide pixels on its longer side, a thumbnail of at most MaxThumbnailSide, both
// WebP and without EXIF (the GPS location and the phone model are dropped). SkiaPhotoProcessor does it.
public interface IPhotoProcessor
{
    // Photo.InvalidFormat when the bytes look like an image but cannot be read, Photo.TooManyPixels when
    // the image is too large to open safely.
    Result<ProcessedPhoto> Process(byte[] content);
}
