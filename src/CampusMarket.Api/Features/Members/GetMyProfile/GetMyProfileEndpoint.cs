using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetMyProfile;

public static class GetMyProfileEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me", Handle)
            .WithName("GetMyProfile")
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<GetMyProfileResponse>, NotFound<Error>>> Handle(
        ClaimsPrincipal user,
        IQueryHandler<GetMyProfileQuery, GetMyProfileResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMyProfileQuery(user.GetMemberId()), ct);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.NotFound(result.Error);
    }
}