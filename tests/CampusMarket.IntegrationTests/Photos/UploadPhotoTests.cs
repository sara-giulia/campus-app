using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CampusMarket.Api.Common.Storage;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Photos;

public sealed class UploadPhotoTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private const long MaxSizeBytes = 2_097_152;

    // Test images: the signature of the format followed by zeros.
    private static readonly byte[] PngImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] JpegImage = [0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    // "RIFF", 4 bytes of size, "WEBP".
    private static readonly byte[] WebPImage = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x00, 0x00, 0x00, 0x00];
    // "GIF89a": a real image, but not an accepted format.
    private static readonly byte[] GifImage = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    [Fact]
    public async Task Given_Seller_When_UploadingPng_Then_Returns201AndSavesRowAndFile()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.Id.Should().NotBeEmpty();
        body.ListingId.Should().Be(listing.Id);
        body.ContentType.Should().Be("image/png");
        body.SizeBytes.Should().Be(PngImage.Length);
        body.Url.Should().Be($"/api/v1/photos/{body.Id}");
        body.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/photos/{body.Id}");

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body.Id);
        photo.ListingId.Should().Be(listing.Id);
        photo.FileName.Should().Be($"{body.Id}.png");
        photo.ContentType.Should().Be("image/png");
        photo.SizeBytes.Should().Be(PngImage.Length);

        var path = Path.Combine(Factory.PhotosPath, listing.Id.ToString(), photo.FileName);
        File.Exists(path).Should().BeTrue();
        (await File.ReadAllBytesAsync(path)).Should().Equal(PngImage);
    }

    public static TheoryData<byte[], string, string> OtherFormats => new()
    {
        { JpegImage, "image/jpeg", ".jpg" },
        { WebPImage, "image/webp", ".webp" }
    };

    [Theory]
    [MemberData(nameof(OtherFormats))]
    public async Task Given_Seller_When_UploadingJpegOrWebP_Then_Returns201WithItsType(byte[] content, string contentType, string extension)
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        using var form = Form(content, $"photo{extension}", contentType);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.ContentType.Should().Be(contentType);
        body.SizeBytes.Should().Be(content.Length);

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body.Id);
        photo.FileName.Should().Be($"{body.Id}{extension}");
        photo.ContentType.Should().Be(contentType);

        var path = Path.Combine(Factory.PhotosPath, listing.Id.ToString(), photo.FileName);
        (await File.ReadAllBytesAsync(path)).Should().Equal(content);
    }

    [Fact]
    public async Task Given_DraftListing_When_SellerUploadsPhoto_Then_Returns201()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: false);
        AuthenticateAs(seller.Id);
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.ListingId.Should().Be(listing.Id);

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(body.Id);
        File.Exists(Path.Combine(Factory.PhotosPath, listing.Id.ToString(), photo.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task Given_GifSentAsPng_When_UploadingPhoto_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        using var form = Form(GifImage, "a.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.InvalidFormat");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_FileOneByteOverTheLimit_When_UploadingPhoto_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        using var form = Form(PngOfSize(MaxSizeBytes + 1), "big.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.TooLarge");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_FileOfExactlyTheLimit_When_UploadingPhoto_Then_Returns201()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        var content = PngOfSize(MaxSizeBytes);
        using var form = Form(content, "big.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        body!.SizeBytes.Should().Be(MaxSizeBytes);

        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.SizeBytes.Should().Be(MaxSizeBytes);
        new FileInfo(Path.Combine(Factory.PhotosPath, listing.Id.ToString(), photo.FileName)).Length.Should().Be(MaxSizeBytes);
    }

    [Fact]
    public async Task Given_NoFileField_When_UploadingPhoto_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        using var form = Form(PngImage, "photo.png", "image/png", fieldName: "image");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.Empty");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_FivePhotosAlready_When_UploadingPhoto_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        var existing = Enumerable.Range(0, 5)
            .Select(i => (object)Photo.Create(seller.Id, listing, i, PngImage, CreatedAt.AddMinutes(i)).Value)
            .ToArray();
        await SeedAsync([seller, listing, .. existing]);
        AuthenticateAs(seller.Id);
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.LimitReached");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(5);
        FilesOnDisk().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_AnotherMember_When_UploadingPhoto_Then_Returns403AndSavesNothing()
    {
        // Arrange
        var (_, listing) = await SeedSellerAndListingAsync(published: true);
        var other = Member.Create("Maria Pop", "maria@student.ro", CreatedAt).Value;
        await SeedAsync(other);
        AuthenticateAs(other.Id);
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotSeller");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_UnknownListing_When_UploadingPhoto_Then_Returns404AndSavesNothing()
    {
        // Arrange
        var (seller, _) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{Guid.NewGuid()}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_NoToken_When_UploadingPhoto_Then_Returns401AndSavesNothing()
    {
        // Arrange
        var (_, listing) = await SeedSellerAndListingAsync(published: true);
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/photos");
        problem.TraceId.Should().NotBeEmpty();

        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Given_JsonBodyInsteadOfForm_When_UploadingPhoto_Then_Returns415AndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/photos", new { file = "photo.png" }, Json);

        // Assert
        // The 415 is written by ASP.NET; its code is not asserted (the section asks only for the status).
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.UnsupportedMediaType);
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/photos");
        problem.TraceId.Should().NotBeEmpty();

        await AssertNothingSavedAsync();
    }

    private async Task<(Member Seller, Listing Listing)> SeedSellerAndListingAsync(bool published)
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        if (published)
            listing.Publish();

        await SeedAsync(seller, listing);
        return (seller, listing);
    }

    // A multipart form with one file part.
    private static MultipartFormDataContent Form(byte[] content, string fileName, string contentType, string fieldName = "file")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, fieldName, fileName } };
    }

    // A PNG signature followed by zeros, of the given total size.
    private static byte[] PngOfSize(long size)
    {
        var content = new byte[size];
        PngImage.AsSpan(0, 8).CopyTo(content);
        return content;
    }

    private string[] FilesOnDisk() =>
        Directory.Exists(Factory.PhotosPath)
            ? Directory.GetFiles(Factory.PhotosPath, "*", SearchOption.AllDirectories)
            : [];

    private async Task AssertNothingSavedAsync()
    {
        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(0);
        FilesOnDisk().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Seller_When_UploadingTwoPhotosInARow_Then_PositionsAreZeroThenOne()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        AuthenticateAs(seller.Id);

        // Act
        using var firstForm = Form(PngImage, "first.png", "image/png");
        var firstResponse = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", firstForm);
        using var secondForm = Form(JpegImage, "second.jpg", "image/jpeg");
        var secondResponse = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", secondForm);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var first = await firstResponse.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        var second = await secondResponse.Content.ReadFromJsonAsync<PhotoResponse>(Json);
        first!.Position.Should().Be(0);
        second!.Position.Should().Be(1);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().ToListAsync());
        saved.Should().HaveCount(2);
        saved.Single(p => p.Id == first.Id).Position.Should().Be(0);
        saved.Single(p => p.Id == second.Id).Position.Should().Be(1);
    }

    [Fact]
    public async Task Given_PhotoStorageFails_When_UploadingPhoto_Then_Returns503StorageFailedAndSavesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync(published: true);
        await using var app = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPhotoStorage>();
                services.AddSingleton<IPhotoStorage, ThrowingPhotoStorage>();
            }));
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(seller.Id));
        using var form = Form(PngImage, "photo.png", "image/png");

        // Act
        var response = await client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.StorageFailed");
        error.Type.Should().Be(ErrorType.Validation);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret");

        await AssertNothingSavedAsync();
    }
}
