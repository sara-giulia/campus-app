using System.Text;
using CampusMarket.Api.Common.CQRS;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Admin.PriceFeedback;

public static class PriceFeedbackEndpoint
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/price-feedback", Handle)
            .WithName("GetPriceFeedback")
            .RequireAuthorization("Admin");
    }

    private static async Task<FileContentHttpResult> Handle(
        IQueryHandler<PriceFeedbackQuery, PriceFeedbackResponse> handler,
        CancellationToken ct)
    {
        // The handler never fails: no sales yet is a valid state (header-only CSV).
        var result = await handler.HandleAsync(new PriceFeedbackQuery(), ct);
        return TypedResults.File(
            Utf8NoBom.GetBytes(result.Value.Csv),
            PriceFeedbackQuery.ContentType,
            PriceFeedbackQuery.FileName);
    }
}
