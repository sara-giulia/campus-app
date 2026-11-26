using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Views.RecordView;

public static class RecordViewEndpoint
{
    public const string VisitorHeader = "X-Visitor-Id";

    // Public: a visitor who is not logged in is counted by the random id in X-Visitor-Id.
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/views", Handle)
            .WithName("RecordListingView");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal user,
        ICommandHandler<RecordViewCommand, bool> handler,
        CancellationToken ct)
    {
        Guid? visitorId = Guid.TryParse(request.Headers[VisitorHeader].ToString(), out var parsed) ? parsed : null;

        var result = await handler.HandleAsync(new RecordViewCommand(id, user.GetMemberIdOrNull(), visitorId), ct);

        if (result.IsFailure)
            return result.Error.Type == ErrorType.NotFound
                ? TypedResults.NotFound(result.Error)
                : TypedResults.BadRequest(result.Error);

        return TypedResults.NoContent();
    }
}
