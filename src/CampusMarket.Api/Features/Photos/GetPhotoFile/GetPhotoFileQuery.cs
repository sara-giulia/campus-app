using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.GetPhotoFile;

public sealed record GetPhotoFileQuery(Guid PhotoId) : IQuery<PhotoFile>;

// The image and its type; the endpoint sends the stream and closes it.
public sealed record PhotoFile(Stream Content, string ContentType);
