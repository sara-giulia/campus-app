using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Moderation.ReportListing;

public static class ReportListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/{listingId:guid}/reports", Handle)
            .WithName("ReportListing")
            .RequireAuthorization();
    }

    private static async Task<Results<Created<ReportListingResponse>, NotFound<Error>, BadRequest<Error>, Conflict<Error>>> Handle(
        Guid listingId,
        ReportListingRequest request,
        ClaimsPrincipal user,
        ICommandHandler<ReportListingCommand, ReportListingResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(
            new ReportListingCommand(user.GetMemberId(), listingId, request.Reason), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        // No Location: a member cannot read reports back, only Admins see them (grouped per listing).
        return TypedResults.Created((string?)null, result.Value);
    }
}
