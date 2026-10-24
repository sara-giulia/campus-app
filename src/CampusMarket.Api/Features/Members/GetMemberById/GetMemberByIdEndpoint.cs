using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
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
        IQueryHandler<GetMemberByIdQuery, GetMemberByIdResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetMemberByIdQuery(request.Id), ct);

        if (result.IsFailure)
            return TypedResults.NotFound(result.Error);

        return TypedResults.Ok(result.Value);
    }
}
