using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Offers.CounterOffer;

public static class CounterOfferEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/counter", Handle)
            .WithName("CounterOffer")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<OfferResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        OfferAmountRequest request,
        ClaimsPrincipal user,
        ICommandHandler<CounterOfferCommand, OfferResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new CounterOfferCommand(user.GetMemberId(), id, request.Amount ?? 0), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        // 200 with the new Pending offer; the conversation (GET /conversations/{id}) lists the whole chain.
        return TypedResults.Ok(result.Value);
    }
}
