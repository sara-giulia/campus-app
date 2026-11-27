using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Photos;
using SkiaSharp;

namespace CampusMarket.Api.Infrastructure.Images;

// IPhotoProcessor with SkiaSharp (MIT). The image is decoded, turned upright by its EXIF orientation,
// made smaller and encoded again as WebP. The Skia encoder writes only the pixels, so every piece of
// metadata of the upload (GPS location, phone model, date) is left behind.
public sealed class SkiaPhotoProcessor : IPhotoProcessor
{
    public const int MaxImageSide = 1600;
    public const int MaxThumbnailSide = 400;
    public const int ImageQuality = 80;
    public const int ThumbnailQuality = 75;

    // About a 40 MP photo. Checked from the header, before any pixel is decoded, so a small file that
    // claims a huge size (a "decompression bomb") is refused without using the memory.
    public const long MaxPixels = 40_000_000;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public Result<ProcessedPhoto> Process(byte[] content)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            return InvalidFormat();

        var size = codec.Info.Size;
        if ((long)size.Width * size.Height > MaxPixels)
            return Result<ProcessedPhoto>.Failure(Error.Validation(
                "Photo.TooManyPixels", $"A photo can have at most {MaxPixels / 1_000_000} megapixels."));

        using var decoded = Decode(codec, MaxImageSide);
        if (decoded is null)
            return InvalidFormat();

        using var image = Render(decoded, codec.EncodedOrigin, MaxImageSide);
        using var thumbnail = Render(image, SKEncodedOrigin.TopLeft, MaxThumbnailSide);

        return new ProcessedPhoto(Encode(image, ImageQuality), Encode(thumbnail, ThumbnailQuality));
    }

    // A JPEG can be decoded straight at n/8 of its size, which saves most of the memory of a phone photo.
    // The codec may round the scale down (0.4 gives 3/8), so a larger one is asked for until the picture
    // is at least maxSide pixels; Render then makes it exactly the right size.
    private static SKImage? Decode(SKCodec codec, int maxSide)
    {
        var size = codec.Info.Size;
        var longer = Math.Max(size.Width, size.Height);
        var needed = Math.Min(maxSide, longer);
        var scale = Math.Min(1f, (float)maxSide / longer);
        var scaled = codec.GetScaledDimensions(scale);
        // At scale 1 the picture is full size, at least `needed`, so the loop always ends.
        while (Math.Max(scaled.Width, scaled.Height) < needed)
        {
            scale = Math.Min(1f, scale * 1.25f);
            scaled = codec.GetScaledDimensions(scale);
        }

        var info = new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            return null;
        }

        bitmap.SetImmutable();
        var image = SKImage.FromBitmap(bitmap);
        bitmap.Dispose();
        return image;
    }

    // Draws the image upright and at most maxSide pixels on its longer side (never larger than it is).
    private static SKImage Render(SKImage source, SKEncodedOrigin origin, int maxSide)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var uprightWidth = swap ? source.Height : source.Width;
        var uprightHeight = swap ? source.Width : source.Height;

        var scale = Math.Min(1f, (float)maxSide / Math.Max(uprightWidth, uprightHeight));
        var width = Math.Max(1, (int)Math.Round(uprightWidth * scale));
        var height = Math.Max(1, (int)Math.Round(uprightHeight * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)width / uprightWidth, (float)height / uprightHeight);
        canvas.Concat(OriginMatrix(origin, source.Width, source.Height));
        canvas.DrawImage(source, 0, 0, Sampling);
        return surface.Snapshot();
    }

    // Maps the stored pixels to the upright picture, for each EXIF orientation (w, h: the stored size).
    internal static SKMatrix OriginMatrix(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity
    };

    private static byte[] Encode(SKImage image, int quality)
    {
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality);
        return encoded.ToArray();
    }

    private static Result<ProcessedPhoto> InvalidFormat() =>
        Result<ProcessedPhoto>.Failure(
            Error.Validation("Photo.InvalidFormat", "Only JPEG, PNG and WebP images are allowed."));
}
