using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Photos;

public sealed class GetPhotoFileTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // A real test image (SkiaSharp), so the processor can decode it.
    private static readonly byte[] PngImage = TestImages.Png(40, 30);

    private static readonly byte[] SeededSvg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'/>");

    [Fact]
    public async Task Given_UploadedPng_When_GettingPhotoFileWithoutToken_Then_Returns200WithTheBytes()
    {
        // Arrange
        var listing = await SeedListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{photo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(86400));

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(photo.Id);
        var path = Path.Combine(Factory.PhotosPath, listing.Id.ToString(), saved.FileName);
        File.Exists(path).Should().BeTrue();
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Given_ProcessedPhoto_When_GettingThumbnailWithoutToken_Then_Returns200WithTheThumbnailAndHeaders()
    {
        // Arrange
        var listing = await SeedListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{photo.Id}/thumbnail");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        AssertPhotoHeaders(response);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.ThumbnailFileName.Should().Be($"{photo.Id}.thumb.webp");
        var thumbnail = await File.ReadAllBytesAsync(Path.Combine(Factory.PhotosPath, listing.Id.ToString(), saved.ThumbnailFileName!));
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(thumbnail);
    }

    [Fact]
    public async Task Given_GeneratedPhoto_When_GettingThumbnail_Then_Returns200WithTheSvgAndThumbnailUrlIsUrl()
    {
        // Arrange
        var listing = await SeedListingAsync();
        var generated = Photo.CreateGenerated(listing, 0, SeededSvg, PhotoFormat.Svg, CreatedAt).Value;
        await SeedAsync(generated);
        var storage = Factory.Services.GetRequiredService<IPhotoStorage>();
        await storage.SaveAsync(listing.Id, generated.FileName, SeededSvg, PhotoFormat.Svg.ContentType);

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{generated.Id}/thumbnail");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        AssertPhotoHeaders(response);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(SeededSvg);

        var listed = await Client.GetFromJsonAsync<GetListingPhotosResponse>($"/api/v1/listings/{listing.Id}/photos", Json);
        var item = listed!.Items.Should().ContainSingle().Which;
        item.Id.Should().Be(generated.Id);
        item.ThumbnailUrl.Should().Be(item.Url);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.ThumbnailFileName.Should().BeNull();
    }

    [Fact]
    public async Task Given_OldPhotoWithoutThumbnail_When_GettingThumbnail_Then_Returns200WithTheImageAndThumbnailUrlIsUrl()
    {
        // Arrange
        var listing = await SeedListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;
        await QueryDbAsync(db => db.Database.ExecuteSqlRawAsync(
            """UPDATE "Photos" SET "ThumbnailFileName" = NULL WHERE "Id" = {0}""", photo.Id));

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{photo.Id}/thumbnail");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        AssertPhotoHeaders(response);
        var image = await File.ReadAllBytesAsync(Path.Combine(Factory.PhotosPath, listing.Id.ToString(), $"{photo.Id}.webp"));
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(image);

        var listed = await Client.GetFromJsonAsync<GetListingPhotosResponse>($"/api/v1/listings/{listing.Id}/photos", Json);
        var item = listed!.Items.Should().ContainSingle().Which;
        item.Url.Should().Be($"/api/v1/photos/{photo.Id}");
        item.ThumbnailUrl.Should().Be(item.Url);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.ThumbnailFileName.Should().BeNull();
    }

    [Fact]
    public async Task Given_ThumbnailFileDeleted_When_GettingThumbnail_Then_Returns404()
    {
        // Arrange
        var listing = await SeedListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;
        var thumbnailPath = Path.Combine(Factory.PhotosPath, listing.Id.ToString(), $"{photo.Id}.thumb.webp");
        File.Delete(thumbnailPath);

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{photo.Id}/thumbnail");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(photo.Id);
        File.Exists(Path.Combine(Factory.PhotosPath, listing.Id.ToString(), $"{photo.Id}.webp")).Should().BeTrue();
    }

    [Fact]
    public async Task Given_UnknownId_When_GettingThumbnail_Then_Returns404()
    {
        // Arrange
        var listing = await SeedListingAsync();
        await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{Guid.NewGuid()}/thumbnail");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(1);
    }

    private static void AssertPhotoHeaders(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(86400));
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Be("default-src 'none'");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task Given_UnknownId_When_GettingPhotoFile_Then_Returns404()
    {
        // Arrange
        var listing = await SeedListingAsync();
        await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_RowWhoseFileWasDeleted_When_GettingPhotoFile_Then_Returns404()
    {
        // Arrange
        var listing = await SeedListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;
        var path = Path.Combine(Factory.PhotosPath, listing.Id.ToString(), $"{photo.Id}.webp");
        File.Delete(path);

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{photo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(photo.Id);
        File.Exists(path).Should().BeFalse();
    }

    private async Task<Listing> SeedListingAsync()
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(seller, listing);
        return listing;
    }

    // Arrange: uploads a photo through the endpoint as the seller, so the file is on disk.
    private async Task<PhotoResponse> UploadAsSellerAsync(Listing listing, byte[] content, string contentType)
    {
        AuthenticateAs(listing.SellerId);
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var form = new MultipartFormDataContent { { file, "file", "photo" } };

        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/photos", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PhotoResponse>(Json))!;
    }
}
