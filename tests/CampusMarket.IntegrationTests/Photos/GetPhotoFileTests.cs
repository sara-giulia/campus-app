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

public sealed class GetPhotoFileTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Test image: the PNG signature followed by zeros.
    private static readonly byte[] PngImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

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
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(86400));
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(PngImage);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(photo.Id);
        File.Exists(Path.Combine(Factory.PhotosPath, listing.Id.ToString(), saved.FileName)).Should().BeTrue();
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
        var path = Path.Combine(Factory.PhotosPath, listing.Id.ToString(), $"{photo.Id}.png");
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
        listing.Publish();
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
