using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Offers.MakeOffer;

public static class MakeOfferEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/offers", Handle)
            .WithName("MakeOffer")
            .RequireAuthorization();
    }

    private static async Task<Results<CreatedAtRoute<MakeOfferResponse>, NotFound<Error>, BadRequest<Error>, Conflict<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        OfferAmountRequest request,
        ClaimsPrincipal user,
        ICommandHandler<MakeOfferCommand, MakeOfferResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new MakeOfferCommand(user.GetMemberId(), id, request.Amount ?? 0), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        // Location points to the conversation, where the offer is listed with the messages.
        return TypedResults.CreatedAtRoute(result.Value, "GetConversation", new { id = result.Value.ConversationId });
    }
}
