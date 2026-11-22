using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Categories.UpdateCategory;

public static class UpdateCategoryEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{category}", Handle)
            .WithName("UpdateCategory")
            .RequireAuthorization("Admin");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>, Conflict<Error>>> Handle(
        string category,
        UpdateCategoryRequest request,
        ICommandHandler<UpdateCategoryCommand, Category> handler,
        CancellationToken ct)
    {
        var command = new UpdateCategoryCommand(category, request.DisplayName, request.Description, request.IsActive);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.NoContent();
    }
}
