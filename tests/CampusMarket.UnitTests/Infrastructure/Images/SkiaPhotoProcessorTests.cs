using System.Text;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Infrastructure.Images;
using SkiaSharp;

namespace CampusMarket.UnitTests.Infrastructure.Images;

public class SkiaPhotoProcessorTests
{
    private static readonly byte[] JpegStartWithGarbage = [0xFF, 0xD8, 0xFF, 0x13, 0x37, 0x42, 0x00, 0x99, 0xAB, 0xCD, 0xEF, 0x01, 0x02, 0x03];

    // A bitmap filled with blue, with a red quarter in the top-left corner, encoded in the given format.
    private static byte[] BuildImage(SKEncodedImageFormat format, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Blue);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Red })
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    // Inserts an EXIF APP1 block (Model "Pixel 9 Pro" and the given Orientation) right after FF D8.
    private static byte[] WithExif(byte[] jpeg, byte orientation)
    {
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x3A,
            .. "Exif\0\0"u8,
            .. "II"u8, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x02, 0x00,
            0x10, 0x01, 0x02, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x26, 0x00, 0x00, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            .. "Pixel 9 Pro\0"u8
        ];
        return [.. jpeg.AsSpan(0, 2), .. app1, .. jpeg.AsSpan(2)];
    }

    // A PNG whose IDAT data does not start with a valid zlib header: the header reads fine, the pixels do not.
    private static byte[] BuildPngWithBrokenPixels()
    {
        var png = BuildImage(SKEncodedImageFormat.Png, 64, 64);
        var idat = png.AsSpan().IndexOf("IDAT"u8);
        png[idat + 4] ^= 0xFF;
        png[idat + 5] ^= 0xFF;
        return png;
    }

    private static (SKEncodedImageFormat Format, int Width, int Height) Describe(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return (codec.EncodedFormat, codec.Info.Width, codec.Info.Height);
    }

    private static SKColor PixelAt(byte[] bytes, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    public void Given_LandscapeJpeg4000x3000_When_Processing_Then_ImageIs1600x1200AndThumbnail400x300AsWebP()
    {
        var content = BuildImage(SKEncodedImageFormat.Jpeg, 4000, 3000);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Describe(result.Value.Image).Should().Be((SKEncodedImageFormat.Webp, 1600, 1200));
        Describe(result.Value.Thumbnail).Should().Be((SKEncodedImageFormat.Webp, 400, 300));
    }

    [Fact]
    public void Given_PortraitPng3000x4000_When_Processing_Then_ImageIs1200x1600AndThumbnail300x400()
    {
        var content = BuildImage(SKEncodedImageFormat.Png, 3000, 4000);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Describe(result.Value.Image).Should().Be((SKEncodedImageFormat.Webp, 1200, 1600));
        Describe(result.Value.Thumbnail).Should().Be((SKEncodedImageFormat.Webp, 300, 400));
    }

    [Fact]
    public void Given_SmallPng300x200_When_Processing_Then_NeitherIsEnlarged()
    {
        var content = BuildImage(SKEncodedImageFormat.Png, 300, 200);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Describe(result.Value.Image).Should().Be((SKEncodedImageFormat.Webp, 300, 200));
        Describe(result.Value.Thumbnail).Should().Be((SKEncodedImageFormat.Webp, 300, 200));
    }

    [Fact]
    public void Given_VeryWidePng3000x2_When_Processing_Then_EachSideIsAtLeastOnePixel()
    {
        var content = BuildImage(SKEncodedImageFormat.Png, 3000, 2);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Describe(result.Value.Image).Should().Be((SKEncodedImageFormat.Webp, 1600, 1));
        Describe(result.Value.Thumbnail).Should().Be((SKEncodedImageFormat.Webp, 400, 1));
    }

    [Fact]
    public void Given_WebP800x600_When_Processing_Then_ReturnsWebPImageAndThumbnail()
    {
        var content = BuildImage(SKEncodedImageFormat.Webp, 800, 600);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Describe(result.Value.Image).Should().Be((SKEncodedImageFormat.Webp, 800, 600));
        Describe(result.Value.Thumbnail).Should().Be((SKEncodedImageFormat.Webp, 400, 300));
    }

    [Theory]
    [InlineData(1, 40, 20, 2, 2, 37, 17)]
    [InlineData(2, 40, 20, 37, 2, 2, 17)]
    [InlineData(3, 40, 20, 37, 17, 2, 2)]
    [InlineData(4, 40, 20, 2, 17, 37, 2)]
    [InlineData(5, 20, 40, 2, 2, 17, 37)]
    [InlineData(6, 20, 40, 17, 2, 2, 37)]
    [InlineData(7, 20, 40, 17, 37, 2, 2)]
    [InlineData(8, 20, 40, 2, 37, 17, 2)]
    public void Given_JpegWithExifOrientation_When_Processing_Then_ImageIsUprightWithRedCornerInPlace(
        byte orientation, int width, int height, int redX, int redY, int blueX, int blueY)
    {
        var content = WithExif(BuildImage(SKEncodedImageFormat.Jpeg, 40, 20), orientation);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Describe(result.Value.Image).Should().Be((SKEncodedImageFormat.Webp, width, height));
        var red = PixelAt(result.Value.Image, redX, redY);
        red.Red.Should().BeGreaterThan(200);
        red.Blue.Should().BeLessThan(60);
        var blue = PixelAt(result.Value.Image, blueX, blueY);
        blue.Blue.Should().BeGreaterThan(200);
        blue.Red.Should().BeLessThan(60);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void Given_JpegWithExif_When_Processing_Then_OutputHasNoExifAndNoPhoneModel(byte orientation)
    {
        var content = WithExif(BuildImage(SKEncodedImageFormat.Jpeg, 40, 20), orientation);

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsSuccess.Should().BeTrue();
        Encoding.Latin1.GetString(result.Value.Image).Should().NotContain("Exif").And.NotContain("Pixel 9 Pro");
        Encoding.Latin1.GetString(result.Value.Thumbnail).Should().NotContain("Exif").And.NotContain("Pixel 9 Pro");
    }

    [Fact]
    public void Given_JpegStartFollowedByGarbage_When_Processing_Then_ReturnsInvalidFormat()
    {
        var result = new SkiaPhotoProcessor().Process(JpegStartWithGarbage);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.InvalidFormat");
        result.Error.Message.Should().Be("Only JPEG, PNG and WebP images are allowed.");
    }

    [Fact]
    public void Given_Gray8Png8000x5001_When_Processing_Then_ReturnsTooManyPixels()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(8000, 5001, SKColorType.Gray8, SKAlphaType.Opaque));
        bitmap.Erase(SKColors.Gray);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var content = encoded.ToArray();

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.TooManyPixels");
        result.Error.Message.Should().Be("A photo can have at most 40 megapixels.");
    }

    [Fact]
    public void Given_PngWhosePixelsCannotBeDecoded_When_Processing_Then_ReturnsInvalidFormat()
    {
        var content = BuildPngWithBrokenPixels();

        var result = new SkiaPhotoProcessor().Process(content);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.InvalidFormat");
        result.Error.Message.Should().Be("Only JPEG, PNG and WebP images are allowed.");
    }

    [Theory]
    [InlineData(1, 0, 0, 40, 20)]
    [InlineData(2, 40, 0, 0, 20)]
    [InlineData(3, 40, 20, 0, 0)]
    [InlineData(4, 0, 20, 40, 0)]
    [InlineData(5, 0, 0, 20, 40)]
    [InlineData(6, 20, 0, 0, 40)]
    [InlineData(7, 20, 40, 0, 0)]
    [InlineData(8, 0, 40, 20, 0)]
    public void Given_Origin_When_BuildingOriginMatrix_Then_MapsStoredCornersToUprightCorners(
        int origin, float topLeftX, float topLeftY, float bottomRightX, float bottomRightY)
    {
        var matrix = SkiaPhotoProcessor.OriginMatrix((SKEncodedOrigin)origin, 40, 20);

        matrix.MapPoint(0, 0).Should().Be(new SKPoint(topLeftX, topLeftY));
        matrix.MapPoint(40, 20).Should().Be(new SKPoint(bottomRightX, bottomRightY));
    }
}
