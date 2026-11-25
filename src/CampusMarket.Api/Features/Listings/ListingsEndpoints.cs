using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.Api.Features.Listings.DeleteListing;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.Api.Features.Listings.PublishListing;
using CampusMarket.Api.Features.Listings.ReserveListing;
using CampusMarket.Api.Features.Listings.CancelReservation;
using CampusMarket.Api.Features.Listings.ConfirmSale;
using CampusMarket.Api.Features.Listings.SuggestPrice;
using CampusMarket.Api.Features.Listings.SuggestCategory;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.Api.Features.Listings.RenewListing;

namespace CampusMarket.Api.Features.Listings;

public static class ListingsEndpoints
{
    public static IEndpointRouteBuilder MapListingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/listings");   // => /api/v1/listings

        CreateListingEndpoint.Map(group);
        GetListingByIdEndpoint.Map(group);
        GetListingsEndpoint.Map(group);
        UpdateListingEndpoint.Map(group);
        DeleteListingEndpoint.Map(group);
        PublishListingEndpoint.Map(group);
        RenewListingEndpoint.Map(group);
        ReserveListingEndpoint.Map(group);
        CancelReservationEndpoint.Map(group);
        ConfirmSaleEndpoint.Map(group);
        SuggestPriceEndpoint.Map(group);
        SuggestCategoryEndpoint.Map(group);
        GetSimilarListingsEndpoint.Map(group);

        return app;
    }
}