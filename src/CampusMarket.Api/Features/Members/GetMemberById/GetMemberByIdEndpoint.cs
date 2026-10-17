using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetMemberById;

public static class GetMemberByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetMemberById");
    }

    private static async Task<Results<Ok<GetMemberByIdResponse>, NotFound<Error>>> Handle(
        [AsParameters] GetMemberByIdRequest request,
        IMemberRepository repository,
        CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(request.Id, ct);

        if (member is null)
            return TypedResults.NotFound(
                Error.NotFound("Member.NotFound", $"Member '{request.Id}' was not found."));

        return TypedResults.Ok(GetMemberByIdResponse.From(member));
    }
}

