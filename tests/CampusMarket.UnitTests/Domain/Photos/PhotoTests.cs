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

    private static readonly byte[] SvgBytes = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();

    private static Listing BuildActiveListing()
    {
        var listing = BuildListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static byte[] BuildPng(long length)
    {
        var bytes = new byte[length];
        PngBytes.AsSpan(0, 8).CopyTo(bytes);
        return bytes;
    }

    private static readonly ProcessedPhoto Processed = new([1, 2, 3, 4], [9, 9]);

    [Fact]
    public void Given_SellerWithPngAndNoPhotos_When_CheckingUpload_Then_ReturnsNull()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, 0, PngBytes);

        error.Should().BeNull();
    }

    [Fact]
    public void Given_Jpeg_When_CheckingUpload_Then_ReturnsNull()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, 0, JpegBytes);

        error.Should().BeNull();
    }

    [Fact]
    public void Given_WebP_When_CheckingUpload_Then_ReturnsNull()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, 0, WebPBytes);

        error.Should().BeNull();
    }

    [Fact]
    public void Given_AnotherMember_When_CheckingUpload_Then_ReturnsForbiddenNotSeller()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(OtherMemberId, listing, 0, PngBytes);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be("Photo.NotSeller");
        error.Message.Should().Be("Only the seller can change the photos of this listing.");
    }

    [Fact]
    public void Given_FourExistingPhotos_When_CheckingUpload_Then_ReturnsNull()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, Photo.MaxPerListing - 1, PngBytes);

        error.Should().BeNull();
    }

    [Fact]
    public void Given_FiveExistingPhotos_When_CheckingUpload_Then_ReturnsLimitReached()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, Photo.MaxPerListing, PngBytes);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Photo.LimitReached");
        error.Message.Should().Be($"A listing can have at most {Photo.MaxPerListing} photos.");
    }

    [Fact]
    public void Given_EmptyContent_When_CheckingUpload_Then_ReturnsEmptyNotInvalidFormat()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, 0, Array.Empty<byte>());

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Photo.Empty");
        error.Message.Should().Be("No file was sent.");
    }

    [Fact]
    public void Given_ContentOfExactlyMaxSize_When_CheckingUpload_Then_ReturnsNull()
    {
        var listing = BuildListing();
        var content = BuildPng(10_485_760);

        var error = Photo.CheckUpload(SellerId, listing, 0, content);

        error.Should().BeNull();
    }

    [Fact]
    public void Given_ContentOneByteOverMaxSize_When_CheckingUpload_Then_ReturnsTooLarge()
    {
        var listing = BuildListing();
        var content = BuildPng(10_485_761);

        var error = Photo.CheckUpload(SellerId, listing, 0, content);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Photo.TooLarge");
        error.Message.Should().Be("A photo must be at most 10 MB.");
    }

    [Fact]
    public void Given_Gif_When_CheckingUpload_Then_ReturnsInvalidFormat()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, 0, GifBytes);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Photo.InvalidFormat");
        error.Message.Should().Be("Only JPEG, PNG and WebP images are allowed.");
    }

    [Fact]
    public void Given_AnotherMemberWithFivePhotos_When_CheckingUpload_Then_ReturnsNotSellerFirst()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(OtherMemberId, listing, Photo.MaxPerListing, PngBytes);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be("Photo.NotSeller");
        error.Message.Should().Be("Only the seller can change the photos of this listing.");
    }

    [Fact]
    public void Given_SellerWithFivePhotosAndEmptyContent_When_CheckingUpload_Then_ReturnsLimitReachedFirst()
    {
        var listing = BuildListing();

        var error = Photo.CheckUpload(SellerId, listing, Photo.MaxPerListing, Array.Empty<byte>());

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Photo.LimitReached");
        error.Message.Should().Be($"A listing can have at most {Photo.MaxPerListing} photos.");
    }

    [Fact]
    public void Given_ProcessedPhoto_When_Creating_Then_ReturnsWebPPhotoWithEveryField()
    {
        var listing = BuildListing();

        var photo = Photo.Create(listing, Processed, Now, 3);

        photo.Id.Should().NotBeEmpty();
        photo.ListingId.Should().Be(listing.Id);
        photo.FileName.Should().Be($"{photo.Id}.webp");
        photo.ThumbnailFileName.Should().Be($"{photo.Id}.thumb.webp");
        photo.ContentType.Should().Be("image/webp");
        photo.SizeBytes.Should().Be(4);
        photo.CreatedAt.Should().Be(Now);
        photo.IsGenerated.Should().BeFalse();
        photo.Position.Should().Be(3);
    }

    [Fact]
    public void Given_NoPosition_When_Creating_Then_PositionIsZero()
    {
        var listing = BuildListing();

        var photo = Photo.Create(listing, Processed, Now);

        photo.Position.Should().Be(0);
    }

    [Fact]
    public void Given_TwoCalls_When_Creating_Then_ReturnsDifferentIds()
    {
        var listing = BuildListing();

        var first = Photo.Create(listing, Processed, Now);
        var second = Photo.Create(listing, Processed, Now, 1);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Given_ActiveListingWithNoPhotos_When_CheckingCanGenerate_Then_ReturnsNull()
    {
        var listing = BuildActiveListing();

        var error = Photo.CheckCanGenerate(listing, 0);

        error.Should().BeNull();
    }

    [Fact]
    public void Given_DraftListing_When_CheckingCanGenerate_Then_ReturnsListingNotActive()
    {
        var listing = BuildListing();

        var error = Photo.CheckCanGenerate(listing, 0);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Photo.ListingNotActive");
        error.Message.Should().Be("Only an active listing gets a generated image.");
    }

    [Fact]
    public void Given_ActiveListingWithOnePhoto_When_CheckingCanGenerate_Then_ReturnsListingHasPhotos()
    {
        var listing = BuildActiveListing();

        var error = Photo.CheckCanGenerate(listing, 1);

        error.Should().NotBeNull();
        error!.Type.Should().Be(ErrorType.Conflict);
        error.Code.Should().Be("Photo.ListingHasPhotos");
        error.Message.Should().Be("The listing already has a photo.");
    }

    [Fact]
    public void Given_ActiveListingWithNoPhotos_When_CreatingGenerated_Then_ReturnsGeneratedPhotoWithEveryField()
    {
        var listing = BuildActiveListing();

        var result = Photo.CreateGenerated(listing, 0, SvgBytes, PhotoFormat.Svg, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.IsGenerated.Should().BeTrue();
        result.Value.FileName.Should().Be($"{result.Value.Id}{PhotoFormat.Svg.Extension}");
        result.Value.ContentType.Should().Be(PhotoFormat.Svg.ContentType);
        result.Value.SizeBytes.Should().Be(SvgBytes.Length);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.ThumbnailFileName.Should().BeNull();
    }

    [Fact]
    public void Given_DraftListing_When_CreatingGenerated_Then_ReturnsListingNotActive()
    {
        var listing = BuildListing();

        var result = Photo.CreateGenerated(listing, 0, SvgBytes, PhotoFormat.Svg, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.ListingNotActive");
        result.Error.Message.Should().Be("Only an active listing gets a generated image.");
    }

    [Fact]
    public void Given_ListingWithExistingPhoto_When_CreatingGenerated_Then_ReturnsListingHasPhotos()
    {
        var listing = BuildActiveListing();

        var result = Photo.CreateGenerated(listing, 1, SvgBytes, PhotoFormat.Svg, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Photo.ListingHasPhotos");
        result.Error.Message.Should().Be("The listing already has a photo.");
    }

    [Fact]
    public void Given_EmptyContent_When_CreatingGenerated_Then_ReturnsEmpty()
    {
        var listing = BuildActiveListing();

        var result = Photo.CreateGenerated(listing, 0, Array.Empty<byte>(), PhotoFormat.Svg, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.Empty");
        result.Error.Message.Should().Be("No file was sent.");
    }

    [Fact]
    public void Given_ContentOneByteOverMaxSize_When_CreatingGenerated_Then_ReturnsTooLarge()
    {
        var listing = BuildActiveListing();
        var content = new byte[Photo.MaxSizeBytes + 1];

        var result = Photo.CreateGenerated(listing, 0, content, PhotoFormat.Svg, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.TooLarge");
        result.Error.Message.Should().Be($"A photo must be at most {Photo.MaxSizeBytes / (1024 * 1024)} MB.");
    }

    [Fact]
    public void Given_ContentOfExactlyMaxSize_When_CreatingGenerated_Then_Succeeds()
    {
        var listing = BuildActiveListing();
        var content = new byte[Photo.MaxSizeBytes];

        var result = Photo.CreateGenerated(listing, 0, content, PhotoFormat.Svg, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.SizeBytes.Should().Be(Photo.MaxSizeBytes);
        result.Value.IsGenerated.Should().BeTrue();
    }
}
