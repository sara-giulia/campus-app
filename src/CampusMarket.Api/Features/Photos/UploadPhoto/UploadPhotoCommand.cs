using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.UploadPhoto;

// The uploader comes from the token; Content is the file (at most Photo.MaxSizeBytes + 1 bytes are read; it is processed before it is saved).
public sealed record UploadPhotoCommand(Guid UploaderId, Guid ListingId, byte[] Content) : ICommand<PhotoResponse>;
