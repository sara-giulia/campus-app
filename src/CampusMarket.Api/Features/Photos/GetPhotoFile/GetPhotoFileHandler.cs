using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos.GetPhotoFile;

public sealed class GetPhotoFileHandler(IPhotoRepository photos, IPhotoStorage storage)
    : IQueryHandler<GetPhotoFileQuery, PhotoFile>
{
    public async Task<Result<PhotoFile>> HandleAsync(GetPhotoFileQuery query, CancellationToken ct)
    {
        var photo = await photos.GetByIdAsync(query.PhotoId, ct);

        // A row whose file is missing (for example a volume that was emptied) is also "not found".
        if (photo is not null && storage.OpenRead(photo.ListingId, photo.FileName) is { } content)
            return new PhotoFile(content, photo.ContentType);

        return Result<PhotoFile>.Failure(
            Error.NotFound("Photo.NotFound", $"Photo '{query.PhotoId}' was not found."));
    }
}
