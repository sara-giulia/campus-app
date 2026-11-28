using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace CampusMarket.IntegrationTests.Photos;

// The real SkiaPhotoProcessor runs: every upload is decoded, turned upright, made smaller and saved as WebP.
public sealed class PhotoProcessingTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private const long MaxSizeBytes = 10_485_760;

    // The big images are made once for the whole class.
    private static readonly Lazy<byte[]> PhonePhoto = new(() => TestImages.Jpeg(4000, 3000, orientation: 6));
    private static readonly Lazy<byte[]> TooManyPixels = new(() => TestImages.Gray8Png(8000, 5001));

    [Fact]
    public async Task Given_RotatedPhonePhotoWithExif_When_Uploading_Then_SavesUprightSmallerWebPWithoutExif()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        var upload = PhonePhoto.Value;
        Encoding.ASCII.GetString(upload).Should().Contain("Exif").And.Contain("Pixel 9 Pro");
        using var form = Form(upload, "phone.jpg", "image/jpeg");

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.ContentType.Should().Be("image/webp");
        body.ThumbnailUrl.Should().Be($"/api/v1/photos/{body.Id}/thumbnail");

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body.Id);
        photo.FileName.Should().Be($"{body.Id}.webp");
        photo.ThumbnailFileName.Should().Be($"{body.Id}.thumb.webp");
        photo.ContentType.Should().Be("image/webp");

        var image = await File.ReadAllBytesAsync(PhotoPath(listing, photo.FileName));
        var thumbnail = await File.ReadAllBytesAsync(PhotoPath(listing, photo.ThumbnailFileName!));
        body.SizeBytes.Should().Be(image.Length);
        photo.SizeBytes.Should().Be(image.Length);

        TestImages.IsWebP(image).Should().BeTrue();
        TestImages.IsWebP(thumbnail).Should().BeTrue();
        foreach (var file in new[] { image, thumbnail })
        {
            var text = Encoding.ASCII.GetString(file);
            text.Should().NotContain("Exif").And.NotContain("Pixel 9 Pro");
        }

        // Orientation 6: the picture is turned 90° clockwise, so the stored top-left corner ends up top-right.
        using var saved = TestImages.Decode(image);
        saved.Width.Should().Be(1200);
        saved.Height.Should().Be(1600);
        IsCorner(saved.GetPixel(saved.Width - 50, 50)).Should().BeTrue();
        IsCorner(saved.GetPixel(50, 50)).Should().BeFalse();
        IsCorner(saved.GetPixel(saved.Width - 50, saved.Height - 50)).Should().BeFalse();

        using var small = TestImages.Decode(thumbnail);
        small.Width.Should().Be(300);
        small.Height.Should().Be(400);
    }

    [Fact]
    public async Task Given_SmallPng_When_Uploading_Then_BothFilesKeepItsSize()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        using var form = Form(TestImages.Png(300, 200), "small.png", "image/png");

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.ContentType.Should().Be("image/webp");

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body.Id);

        // Never enlarged: both are 300x200.
        using var image = TestImages.Decode(await File.ReadAllBytesAsync(PhotoPath(listing, photo.FileName)));
        using var thumbnail = TestImages.Decode(await File.ReadAllBytesAsync(PhotoPath(listing, photo.ThumbnailFileName!)));
        (image.Width, image.Height).Should().Be((300, 200));
        (thumbnail.Width, thumbnail.Height).Should().Be((300, 200));
        IsCorner(image.GetPixel(5, 5)).Should().BeTrue();
    }

    public static TheoryData<string, string> OtherImages => new()
    {
        { "webp", "image/webp" },
        { "transparent-png", "image/png" }
    };

    [Theory]
    [MemberData(nameof(OtherImages))]
    public async Task Given_WebPOrTransparentPng_When_Uploading_Then_Returns201AndSavesWebP(string kind, string contentType)
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        var content = kind == "webp" ? TestImages.WebP(120, 80) : TestImages.TransparentPng(120, 80);
        using var form = Form(content, $"{kind}.img", contentType);

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.ContentType.Should().Be("image/webp");

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body.Id);
        var image = await File.ReadAllBytesAsync(PhotoPath(listing, photo.FileName));
        TestImages.IsWebP(image).Should().BeTrue();
        TestImages.IsWebP(await File.ReadAllBytesAsync(PhotoPath(listing, photo.ThumbnailFileName!))).Should().BeTrue();
        body.SizeBytes.Should().Be(image.Length);
    }

    [Fact]
    public async Task Given_JpegStartFollowedByGarbage_When_Uploading_Then_Returns400InvalidFormatAndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        byte[] garbage = [0xFF, 0xD8, 0xFF, .. "this is not a picture at all"u8];
        using var form = Form(garbage, "broken.jpg", "image/jpeg");

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        await AssertValidationErrorAsync(response, "Photo.InvalidFormat");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_PngWithMoreThan40MegapixelsByHeader_When_Uploading_Then_Returns400TooManyPixelsAndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        using var form = Form(TooManyPixels.Value, "huge.png", "image/png");

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        await AssertValidationErrorAsync(response, "Photo.TooManyPixels");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_RealPngPaddedToExactlyTheLimit_When_Uploading_Then_Returns201()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        using var form = Form(TestImages.PaddedPng(MaxSizeBytes), "padded.png", "image/png");

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body!.Id);
        var image = await File.ReadAllBytesAsync(PhotoPath(listing, photo.FileName));
        TestImages.IsWebP(image).Should().BeTrue();
        body.SizeBytes.Should().Be(image.Length);
        photo.SizeBytes.Should().Be(image.Length);
    }

    [Fact]
    public async Task Given_RealPngPaddedToOneByteOverTheLimit_When_Uploading_Then_Returns400TooLargeAndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);
        using var form = Form(TestImages.PaddedPng(MaxSizeBytes + 1), "padded.png", "image/png");

        // Act
        var response = await Client.PostAsync(PhotosUrl(listing), form);

        // Assert
        await AssertValidationErrorAsync(response, "Photo.TooLarge");
        await AssertNothingSavedAsync();
    }

    private async Task<(Member Seller, Listing Listing)> SeedSellerAndListingAsync()
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(seller, listing);
        return (seller, listing);
    }

    private static string PhotosUrl(Listing listing) => $"/api/v1/listings/{listing.Id}/photos";

    private string PhotoPath(Listing listing, string fileName) =>
        Path.Combine(Factory.PhotosPath, listing.Id.ToString(), fileName);

    private static MultipartFormDataContent Form(byte[] content, string fileName, string contentType)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    // Red within the lossy WebP tolerance.
    private static bool IsCorner(SKColor color) => color.Red > 180 && color.Green < 80 && color.Blue < 80;

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(code);
        error.Type.Should().Be(ErrorType.Validation);
    }

    private async Task AssertNothingSavedAsync()
    {
        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(0);
        var files = Directory.Exists(Factory.PhotosPath)
            ? Directory.GetFiles(Factory.PhotosPath, "*", SearchOption.AllDirectories)
            : [];
        files.Should().BeEmpty();
    }
}
