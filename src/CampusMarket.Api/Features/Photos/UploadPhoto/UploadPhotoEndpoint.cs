using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Photos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Photos.UploadPhoto;

public static class UploadPhotoEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        // multipart/form-data with the image in the "file" field. The token is in the Authorization
        // header (no cookies), so the antiforgery check of forms is not needed.
        group.MapPost("/{listingId:guid}/photos", Handle)
            .WithName("UploadPhoto")
            .RequireAuthorization()
            .DisableAntiforgery();
    }

    private static async Task<Results<CreatedAtRoute<PhotoResponse>, NotFound<Error>, BadRequest<Error>, JsonHttpResult<Error>>> Handle(
        Guid listingId,
        IFormFile? file,
        ClaimsPrincipal user,
        ICommandHandler<UploadPhotoCommand, PhotoResponse> handler,
        CancellationToken ct)
    {
        var content = await ReadAtMostAsync(file, Photo.MaxSizeBytes + 1, ct);
        var result = await handler.HandleAsync(new UploadPhotoCommand(user.GetMemberId(), listingId, content), ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                ErrorType.Forbidden => TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.CreatedAtRoute(result.Value, "GetPhotoFile", new { id = result.Value.Id });
    }

    // One byte more than the limit is enough to know the file is too large, so a big upload is never
    // held in memory. No file → empty content (Photo.Empty).
    private static async Task<byte[]> ReadAtMostAsync(IFormFile? file, long maxBytes, CancellationToken ct)
    {
        if (file is null)
            return [];

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while (buffer.Length < maxBytes
               && (read = await input.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, maxBytes - buffer.Length)), ct)) > 0)
            buffer.Write(chunk, 0, read);

        return buffer.ToArray();
    }
}
