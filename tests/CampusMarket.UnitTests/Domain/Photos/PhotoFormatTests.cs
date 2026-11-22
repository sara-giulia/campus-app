using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.UnitTests.Domain.Photos;

public class PhotoFormatTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] WebPBytes = [0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0, 0, 0, 0];
    private static readonly byte[] WebPExactly12Bytes = [0x52, 0x49, 0x46, 0x46, 0x04, 0, 0, 0, 0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] GifBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] RandomBytes = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0, 0x11, 0x22, 0x33, 0x44];
    private static readonly byte[] RiffOnlyBytes = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WaveBytes = [0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x41, 0x56, 0x45, 0, 0, 0, 0];
    private static readonly byte[] JpegFirstTwoBytes = [0xFF, 0xD8];

    [Fact]
    public void Given_JpegStart_When_Detecting_Then_ReturnsJpegWithExtensionAndContentType()
    {
        var format = PhotoFormat.Detect(JpegBytes);

        format.Should().Be(PhotoFormat.Jpeg);
        format!.Extension.Should().Be(".jpg");
        format.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public void Given_PngStart_When_Detecting_Then_ReturnsPngWithExtensionAndContentType()
    {
        var format = PhotoFormat.Detect(PngBytes);

        format.Should().Be(PhotoFormat.Png);
        format!.Extension.Should().Be(".png");
        format.ContentType.Should().Be("image/png");
    }

    [Fact]
    public void Given_WebPStart_When_Detecting_Then_ReturnsWebPWithExtensionAndContentType()
    {
        var format = PhotoFormat.Detect(WebPBytes);

        format.Should().Be(PhotoFormat.WebP);
        format!.Extension.Should().Be(".webp");
        format.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public void Given_WebPOfExactly12Bytes_When_Detecting_Then_ReturnsWebP()
    {
        var format = PhotoFormat.Detect(WebPExactly12Bytes);

        format.Should().Be(PhotoFormat.WebP);
        format!.Extension.Should().Be(".webp");
        format.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public void Given_GifStart_When_Detecting_Then_ReturnsNull()
    {
        var format = PhotoFormat.Detect(GifBytes);

        format.Should().BeNull();
    }

    [Fact]
    public void Given_RandomBytes_When_Detecting_Then_ReturnsNull()
    {
        var format = PhotoFormat.Detect(RandomBytes);

        format.Should().BeNull();
    }

    [Fact]
    public void Given_EmptyArray_When_Detecting_Then_ReturnsNull()
    {
        var format = PhotoFormat.Detect(Array.Empty<byte>());

        format.Should().BeNull();
    }

    [Fact]
    public void Given_OnlyRiff_When_Detecting_Then_ReturnsNull()
    {
        var format = PhotoFormat.Detect(RiffOnlyBytes);

        format.Should().BeNull();
    }

    [Fact]
    public void Given_RiffWithWave_When_Detecting_Then_ReturnsNull()
    {
        var format = PhotoFormat.Detect(WaveBytes);

        format.Should().BeNull();
    }

    [Fact]
    public void Given_FirstTwoBytesOfJpeg_When_Detecting_Then_ReturnsNull()
    {
        var format = PhotoFormat.Detect(JpegFirstTwoBytes);

        format.Should().BeNull();
    }
}
