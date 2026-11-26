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

public sealed class DeletePhotoTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Test images: the signature of the format followed by zeros.
    private static readonly byte[] PngImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] JpegImage = [0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    [Fact]
    public async Task Given_TwoPhotos_When_SellerDeletesOne_Then_Returns204AndOnlyThatOneIsGone()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var deleted = await UploadAsSellerAsync(listing, PngImage, "image/png");
        var kept = await UploadAsSellerAsync(listing, JpegImage, "image/jpeg");
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/photos/{deleted.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var photos = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().ToListAsync());
        photos.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
        File.Exists(PhotoPath(listing.Id, $"{deleted.Id}.png")).Should().BeFalse();
        File.Exists(PhotoPath(listing.Id, $"{kept.Id}.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task Given_DeletedPhoto_When_GettingPhotoFile_Then_Returns404()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        AuthenticateAs(seller.Id);
        var deleteResponse = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/photos/{photo.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/photos/{photo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMember_When_DeletingPhoto_Then_Returns403AndRemovesNothing()
    {
        // Arrange
        var (_, listing) = await SeedSellerAndListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        var other = Member.Create("Maria Pop", "maria@student.ro", CreatedAt).Value;
        await SeedAsync(other);
        AuthenticateAs(other.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/photos/{photo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotSeller");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertPhotoStillThereAsync(listing.Id, photo.Id);
    }

    [Fact]
    public async Task Given_PhotoOfAnotherListing_When_DeletingPhoto_Then_Returns404AndRemovesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var otherListing = Listing.Create(seller.Id, "Calculator stiintific", Category.Electronics, Condition.Good, 120m, CreatedAt).Value;
        otherListing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(otherListing);
        var otherPhoto = await UploadAsSellerAsync(otherListing, PngImage, "image/png");
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/photos/{otherPhoto.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertPhotoStillThereAsync(otherListing.Id, otherPhoto.Id);
    }

    [Fact]
    public async Task Given_UnknownPhoto_When_DeletingPhoto_Then_Returns404AndRemovesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/photos/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertPhotoStillThereAsync(listing.Id, photo.Id);
    }

    [Fact]
    public async Task Given_UnknownListing_When_DeletingPhoto_Then_Returns404AndRemovesNothing()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{Guid.NewGuid()}/photos/{photo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertPhotoStillThereAsync(listing.Id, photo.Id);
    }

    [Fact]
    public async Task Given_NoToken_When_DeletingPhoto_Then_Returns401AndRemovesNothing()
    {
        // Arrange
        var (_, listing) = await SeedSellerAndListingAsync();
        var photo = await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/photos/{photo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/photos/{photo.Id}");
        problem.TraceId.Should().NotBeEmpty();

        await AssertPhotoStillThereAsync(listing.Id, photo.Id);
    }

    [Fact]
    public async Task Given_ListingWithPhotos_When_SellerDeletesListing_Then_Returns204AndPhotosAndFolderAreGone()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        await UploadAsSellerAsync(listing, PngImage, "image/png");
        await UploadAsSellerAsync(listing, JpegImage, "image/jpeg");
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync(p => p.ListingId == listing.Id));
        count.Should().Be(0);
        Directory.Exists(Path.Combine(Factory.PhotosPath, listing.Id.ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task Given_ListingThatNeverHadPhotos_When_SellerDeletesListing_Then_Returns204()
    {
        // Arrange
        var (seller, listing) = await SeedSellerAndListingAsync();
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var exists = await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == listing.Id));
        exists.Should().BeFalse();
        Directory.Exists(Path.Combine(Factory.PhotosPath, listing.Id.ToString())).Should().BeFalse();
    }

    private async Task<(Member Seller, Listing Listing)> SeedSellerAndListingAsync()
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(seller, listing);
        return (seller, listing);
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

    private string PhotoPath(Guid listingId, string fileName) =>
        Path.Combine(Factory.PhotosPath, listingId.ToString(), fileName);

    private async Task AssertPhotoStillThereAsync(Guid listingId, Guid photoId)
    {
        var photo = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().SingleAsync());
        photo.Id.Should().Be(photoId);
        photo.ListingId.Should().Be(listingId);
        File.Exists(PhotoPath(listingId, $"{photoId}.png")).Should().BeTrue();
    }
}
