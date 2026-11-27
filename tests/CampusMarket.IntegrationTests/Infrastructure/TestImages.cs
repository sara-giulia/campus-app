using SkiaSharp;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Real images made with SkiaSharp, so the API's SkiaPhotoProcessor can decode them. Each one is one colour
// (Background) with a Corner-coloured block in its top-left quarter, so a test can see where the corner went.
public static class TestImages
{
    public static readonly SKColor Background = SKColors.Blue;
    public static readonly SKColor Corner = SKColors.Red;

    public static byte[] Png(int width, int height) => Encode(Draw(width, height), SKEncodedImageFormat.Png);

    // orientation: the EXIF orientation (1-8) put in an APP1 segment, together with the phone model "Pixel 9 Pro".
    public static byte[] Jpeg(int width, int height, int? orientation = null)
    {
        var jpeg = Encode(Draw(width, height), SKEncodedImageFormat.Jpeg);
        return orientation is null ? jpeg : WithExif(jpeg, (byte)orientation.Value);
    }

    public static byte[] WebP(int width, int height) => Encode(Draw(width, height), SKEncodedImageFormat.Webp);

    // A PNG whose right half is fully transparent.
    public static byte[] TransparentPng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = Background })
            canvas.DrawRect(0, 0, width / 2f, height, paint);
        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    // A one-colour grey PNG; compresses to a small file even for a huge size.
    public static byte[] Gray8Png(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
        bitmap.Erase(SKColors.Gray);
        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    // A real PNG followed by zeros, exactly totalSize bytes long.
    public static byte[] PaddedPng(long totalSize)
    {
        var png = Png(40, 30);
        var content = new byte[totalSize];
        png.CopyTo(content, 0);
        return content;
    }

    public static bool IsWebP(byte[] content)
    {
        using var codec = SKCodec.Create(new MemoryStream(content));
        return codec?.EncodedFormat == SKEncodedImageFormat.Webp;
    }

    public static SKBitmap Decode(byte[] content) =>
        SKBitmap.Decode(content) ?? throw new InvalidOperationException("The image cannot be decoded.");

    private static SKBitmap Draw(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(Background);
        using var paint = new SKPaint { Color = Corner };
        canvas.DrawRect(0, 0, width / 4f, height / 4f, paint);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        using (var data = bitmap.Encode(format, 90))
            return data.ToArray();
    }

    // After FF D8: an APP1 segment with "Exif\0\0", a little-endian TIFF header and one IFD with the
    // Model ("Pixel 9 Pro") and the Orientation.
    private static byte[] WithExif(byte[] jpeg, byte orientation)
    {
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x3A,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x02, 0x00,
            0x10, 0x01, 0x02, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x26, 0x00, 0x00, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            .. "Pixel 9 Pro\0"u8
        ];
        return [jpeg[0], jpeg[1], .. app1, .. jpeg.AsSpan(2)];
    }
}
