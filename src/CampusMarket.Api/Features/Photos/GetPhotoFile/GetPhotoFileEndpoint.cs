using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Photos.GetPhotoFile;

public static class GetPhotoFileEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        // Public, so an <img src> can load it without a token.
        group.MapGet("/{id:guid}", (Guid id, HttpContext context, IQueryHandler<GetPhotoFileQuery, PhotoFile> handler, CancellationToken ct) =>
                Handle(new GetPhotoFileQuery(id), context, handler, ct))
            .WithName("GetPhotoFile");

        // The small version for the cards and lists (the image itself when the photo has no thumbnail).
        group.MapGet("/{id:guid}/thumbnail", (Guid id, HttpContext context, IQueryHandler<GetPhotoFileQuery, PhotoFile> handler, CancellationToken ct) =>
                Handle(new GetPhotoFileQuery(id, Thumbnail: true), context, handler, ct))
            .WithName("GetPhotoThumbnail");
    }

    private static async Task<Results<FileStreamHttpResult, NotFound<Error>>> Handle(
        GetPhotoFileQuery query,
        HttpContext context,
        IQueryHandler<GetPhotoFileQuery, PhotoFile> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(query, ct);

        if (result.IsFailure)
            return TypedResults.NotFound(result.Error);

        // A photo never changes (a new one gets a new id), so the browser can keep it for a day.
        context.Response.Headers.CacheControl = "public, max-age=86400";
        // A generated image is an SVG, which a browser could run scripts from when opened directly: the
        // policy allows nothing at all (the SVG draws with attributes, not style sheets, so it still shows),
        // and the type is never guessed from the content.
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.Stream(result.Value.Content, result.Value.ContentType);
    }
}
