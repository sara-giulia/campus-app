using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.DeleteMember;

public static class DeleteMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteMember")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, JsonHttpResult<Error>>> Handle(
        [AsParameters] DeleteMemberRequest request,
        ClaimsPrincipal user,
        ICommandHandler<DeleteMemberCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteMemberCommand(request.Id, user.GetMemberId(), user.IsAdmin()), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.NotFound(result.Error)
            };

        return TypedResults.NoContent();
    }
}
