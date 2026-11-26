using CampusMarket.Api.Features.Offers.AcceptOffer;
using CampusMarket.Api.Features.Offers.CounterOffer;
using CampusMarket.Api.Features.Offers.MakeOffer;
using CampusMarket.Api.Features.Offers.RejectOffer;
using CampusMarket.Api.Features.Offers.WithdrawOffer;

namespace CampusMarket.Api.Features.Offers;

public static class OffersEndpoints
{
    public static IEndpointRouteBuilder MapOffersEndpoints(this IEndpointRouteBuilder app)
    {
        // The buyer proposes a price: /api/v1/listings/{id}/offers
        MakeOfferEndpoint.Map(app.MapGroup("/listings"));

        // Answering: /api/v1/offers/{id}/accept, /reject, /counter, /withdraw.
        // The offers themselves are read with the conversation (GET /api/v1/conversations/{id}).
        var offers = app.MapGroup("/offers");
        AcceptOfferEndpoint.Map(offers);
        RejectOfferEndpoint.Map(offers);
        CounterOfferEndpoint.Map(offers);
        WithdrawOfferEndpoint.Map(offers);

        return app;
    }
}
