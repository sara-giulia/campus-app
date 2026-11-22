using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.UnitTests.Domain.Photos;

public class PhotoTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");

    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] WebPBytes = [0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0, 0, 0, 0];
    private static readonly byte[] GifBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static Listing BuildListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static byte[] BuildPng(long length)
    {
        var bytes = new byte[length];
        PngBytes.AsSpan(0, 8).CopyTo(bytes);
        return bytes;
    }

    [Fact]
    public void Given_SellerWithPngAndNoPhotos_When_Creating_Then_ReturnsPhotoWithEveryField()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, 0, PngBytes, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.FileName.Should().Be($"{result.Value.Id}.png");
        result.Value.ContentType.Should().Be("image/png");
        result.Value.SizeBytes.Should().Be(PngBytes.Length);
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Given_Jpeg_When_Creating_Then_UsesJpgExtensionAndJpegContentType()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, 0, JpegBytes, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be($"{result.Value.Id}.jpg");
        result.Value.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public void Given_WebP_When_Creating_Then_UsesWebpExtensionAndWebpContentType()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, 0, WebPBytes, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be($"{result.Value.Id}.webp");
        result.Value.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public void Given_AnotherMember_When_Creating_Then_ReturnsForbiddenNotSeller()
    {
        var listing = BuildListing();

        var result = Photo.Create(OtherMemberId, listing, 0, PngBytes, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
    }

    [Fact]
    public void Given_FourExistingPhotos_When_Creating_Then_Succeeds()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, Photo.MaxPerListing - 1, PngBytes, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public void Given_FiveExistingPhotos_When_Creating_Then_ReturnsLimitReached()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, Photo.MaxPerListing, PngBytes, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.LimitReached");
        result.Error.Message.Should().Be($"A listing can have at most {Photo.MaxPerListing} photos.");
    }

    [Fact]
    public void Given_EmptyContent_When_Creating_Then_ReturnsEmptyNotInvalidFormat()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, 0, Array.Empty<byte>(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.Empty");
        result.Error.Message.Should().Be("No file was sent.");
    }

    [Fact]
    public void Given_ContentOfExactlyMaxSize_When_Creating_Then_Succeeds()
    {
        var listing = BuildListing();
        var content = BuildPng(Photo.MaxSizeBytes);

        var result = Photo.Create(SellerId, listing, 0, content, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.SizeBytes.Should().Be(Photo.MaxSizeBytes);
    }

    [Fact]
    public void Given_ContentOneByteOverMaxSize_When_Creating_Then_ReturnsTooLarge()
    {
        var listing = BuildListing();
        var content = BuildPng(Photo.MaxSizeBytes + 1);

        var result = Photo.Create(SellerId, listing, 0, content, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.TooLarge");
        result.Error.Message.Should().Be($"A photo must be at most {Photo.MaxSizeBytes / (1024 * 1024)} MB.");
    }

    [Fact]
    public void Given_Gif_When_Creating_Then_ReturnsInvalidFormat()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, 0, GifBytes, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.InvalidFormat");
        result.Error.Message.Should().Be("Only JPEG, PNG and WebP images are allowed.");
    }

    [Fact]
    public void Given_AnotherMemberWithFivePhotos_When_Creating_Then_ReturnsNotSellerFirst()
    {
        var listing = BuildListing();

        var result = Photo.Create(OtherMemberId, listing, Photo.MaxPerListing, PngBytes, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
    }

    [Fact]
    public void Given_SellerWithFivePhotosAndEmptyContent_When_Creating_Then_ReturnsLimitReachedFirst()
    {
        var listing = BuildListing();

        var result = Photo.Create(SellerId, listing, Photo.MaxPerListing, Array.Empty<byte>(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.LimitReached");
        result.Error.Message.Should().Be($"A listing can have at most {Photo.MaxPerListing} photos.");
    }

    [Fact]
    public void Given_TwoCalls_When_Creating_Then_ReturnsDifferentIds()
    {
        var listing = BuildListing();

        var first = Photo.Create(SellerId, listing, 0, PngBytes, Now);
        var second = Photo.Create(SellerId, listing, 1, PngBytes, Now);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Id.Should().NotBe(second.Value.Id);
    }
}
