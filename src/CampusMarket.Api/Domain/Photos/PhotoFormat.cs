namespace CampusMarket.Api.Domain.Photos;

// The three accepted image formats, recognised by the bytes every file of that format starts with.
public sealed record PhotoFormat(string Extension, string ContentType)
{
    public static readonly PhotoFormat Jpeg = new(".jpg", "image/jpeg");
    public static readonly PhotoFormat Png = new(".png", "image/png");
    public static readonly PhotoFormat WebP = new(".webp", "image/webp");

    private static readonly byte[] JpegStart = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngStart = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    // JPEG: FF D8 FF. PNG: 89 'PNG' 0D 0A 1A 0A. WebP: 'RIFF', 4 bytes of size, 'WEBP'. Anything else: null.
    public static PhotoFormat? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(JpegStart))
            return Jpeg;

        if (content.StartsWith(PngStart))
            return Png;

        if (content.Length >= 12 && content.StartsWith(Riff) && content.Slice(8, 4).SequenceEqual(Webp))
            return WebP;

        return null;
    }
}
