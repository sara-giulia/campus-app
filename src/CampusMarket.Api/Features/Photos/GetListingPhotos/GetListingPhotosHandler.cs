using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Features.Photos.GetListingPhotos;

// Like GET /listings/{id}: anyone who can open the listing sees its photos (a draft only its seller or an Admin).
public sealed class GetListingPhotosHandler(IListingRepository listings, IPhotoRepository photos)
    : IQueryHandler<GetListingPhotosQuery, GetListingPhotosResponse>
{
    public async Task<Result<GetListingPhotosResponse>> HandleAsync(GetListingPhotosQuery query, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(query.ListingId, ct);
        if (listing is null || !listing.IsVisibleTo(query.RequesterId, query.IsAdmin))
            return Result<GetListingPhotosResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{query.ListingId}' was not found."));

        var items = (await photos.GetByListingAsync(query.ListingId, ct))
            .OrderBy(p => p.Position)
            .ThenBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(PhotoResponse.From)
            .ToList();

        return new GetListingPhotosResponse(items, items.Count);
    }
}
