namespace CampusMarket.Api.Features.Listings;

public static class ListingsEndpoints
{
    public static IEndpointRouteBuilder MapListingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/listings");   // => /api/v1/listings

        // Fiecare slice isi adauga aici apelul propriu, de exemplu:
        // CreateListing.Endpoint.Map(group);

        return app;
    }
}