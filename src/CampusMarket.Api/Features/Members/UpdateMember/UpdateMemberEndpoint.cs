using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.UpdateMember;

public static class UpdateMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateMember");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, Conflict<Error>, JsonHttpResult<Error>>> Handle(
        Guid id,
        UpdateMemberRequest request,
        ICommandHandler<UpdateMemberCommand, Guid> handler,
        CancellationToken ct)
    {
        var command = new UpdateMemberCommand(id, request.Name, request.Email);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.NoContent();
    }
}
