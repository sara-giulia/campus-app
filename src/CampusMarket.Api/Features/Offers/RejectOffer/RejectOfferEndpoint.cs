using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Offers.RejectOffer;

public static class RejectOfferEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/reject", Handle)
            .WithName("RejectOffer")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<OfferResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<RejectOfferCommand, OfferResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new RejectOfferCommand(user.GetMemberId(), id), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.Ok(result.Value);
    }
}
