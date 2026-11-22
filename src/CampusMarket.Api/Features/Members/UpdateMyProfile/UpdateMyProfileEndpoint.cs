using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.UpdateMyProfile;

public static class UpdateMyProfileEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/me", Handle)
            .WithName("UpdateMyProfile")
            .RequireAuthorization();
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        UpdateMyProfileRequest request,
        ClaimsPrincipal user,
        ICommandHandler<UpdateMyProfileCommand, Guid> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateMyProfileCommand(user.GetMemberId(), request.Name), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        return TypedResults.NoContent();
    }
}