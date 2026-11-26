using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Admin.TrainingExamples;

public static class TrainingExamplesEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/training-examples", Handle)
            .WithName("GetTrainingExamples")
            .RequireAuthorization("Admin");
    }

    private static async Task<Ok<TrainingExamplesResponse>> Handle(
        IQueryHandler<TrainingExamplesQuery, TrainingExamplesResponse> handler,
        CancellationToken ct)
    {
        // The handler never fails: no examples yet is a valid state (count 0, empty lists).
        var result = await handler.HandleAsync(new TrainingExamplesQuery(), ct);
        return TypedResults.Ok(result.Value);
    }
}
