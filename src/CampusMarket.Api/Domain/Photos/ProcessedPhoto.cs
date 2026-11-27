namespace CampusMarket.Api.Domain.Photos;

// An uploaded photo after IPhotoProcessor: the image made smaller and the thumbnail for the lists, both
// WebP and without any metadata (no GPS location, no phone model).
public sealed record ProcessedPhoto(byte[] Image, byte[] Thumbnail);
