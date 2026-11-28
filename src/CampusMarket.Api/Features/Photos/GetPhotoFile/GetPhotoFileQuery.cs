using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.GetPhotoFile;

// Thumbnail: the small file for the lists; a photo without one (generated, or uploaded before the
// thumbnails) sends its image instead, so the URL always works.
public sealed record GetPhotoFileQuery(Guid PhotoId, bool Thumbnail = false) : IQuery<PhotoFile>;

// The image and its type; the endpoint sends the stream and closes it.
public sealed record PhotoFile(Stream Content, string ContentType);
