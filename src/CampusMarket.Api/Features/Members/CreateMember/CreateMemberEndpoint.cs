using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.CreateMember;

public static class CreateMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateMember");
    }

    private static async Task<Results<CreatedAtRoute<CreateMemberResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        CreateMemberRequest request,
        ICommandHandler<CreateMemberCommand, CreateMemberResponse> handler,
        CancellationToken ct)
    {
        var command = new CreateMemberCommand(request.Name, request.Email);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.CreatedAtRoute(
            result.Value, "GetMemberById", new { id = result.Value.Id });
    }
}
