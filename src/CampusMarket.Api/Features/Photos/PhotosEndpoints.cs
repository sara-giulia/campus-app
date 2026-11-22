using CampusMarket.Api.Features.Photos.DeletePhoto;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.Api.Features.Photos.GetPhotoFile;
using CampusMarket.Api.Features.Photos.UploadPhoto;

namespace CampusMarket.Api.Features.Photos;

public static class PhotosEndpoints
{
    public static IEndpointRouteBuilder MapPhotosEndpoints(this IEndpointRouteBuilder app)
    {
        // The photos of a listing: /api/v1/listings/{listingId}/photos
        var listings = app.MapGroup("/listings");
        UploadPhotoEndpoint.Map(listings);
        GetListingPhotosEndpoint.Map(listings);
        DeletePhotoEndpoint.Map(listings);

        // The image itself: /api/v1/photos/{id}
        GetPhotoFileEndpoint.Map(app.MapGroup("/photos"));

        return app;
    }
}
