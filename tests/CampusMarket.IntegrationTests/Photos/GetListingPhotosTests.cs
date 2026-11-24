using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Photos;

public sealed class GetListingPhotosTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Test images: the signature of the format followed by zeros.
    private static readonly byte[] PngImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] JpegImage = [0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    // "RIFF", 4 bytes of size, "WEBP".
    private static readonly byte[] WebPImage = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x00, 0x00, 0x00, 0x00];

    [Fact]
    public async Task Given_ThreeUploadedPhotos_When_GettingListingPhotosWithoutToken_Then_Returns200InUploadOrder()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller, "Manual POO");
        await SeedAsync(seller, listing);
        var first = await UploadAsSellerAsync(listing, PngImage, "image/png");
        var second = await UploadAsSellerAsync(listing, JpegImage, "image/jpeg");
        var third = await UploadAsSellerAsync(listing, WebPImage, "image/webp");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listing.Id}/photos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingPhotosResponse>(Json);
        body!.Count.Should().Be(3);
        // The upload responses carry the clock's 100 ns ticks; the listing returns what PostgreSQL saved (microseconds).
        body.Items.Should().BeEquivalentTo(new[] { first, second, third }, options => options
            .WithStrictOrdering()
            .Using<DateTimeOffset>(ctx => ctx.Subject.Should().Be(ctx.Expectation.ToMicroseconds()))
            .WhenTypeIs<DateTimeOffset>());
        body.Items.Select(p => p.ContentType).Should().Equal("image/png", "image/jpeg", "image/webp");
        body.Items.Should().OnlyContain(p => p.ListingId == listing.Id && p.Url == $"/api/v1/photos/{p.Id}");

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_ListingWithoutPhotos_When_GettingListingPhotos_Then_Returns200WithEmptyItems()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller, "Manual POO");
        await SeedAsync(seller, listing);

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listing.Id}/photos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingPhotosResponse>(Json);
        body!.Count.Should().Be(0);
        body.Items.Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_PhotosOfAnotherListing_When_GettingListingPhotos_Then_TheyAreNotReturned()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller, "Manual POO");
        var otherListing = PublishedListing(seller, "Calculator stiintific");
        await SeedAsync(seller, listing, otherListing);
        var own = await UploadAsSellerAsync(listing, PngImage, "image/png");
        await UploadAsSellerAsync(otherListing, JpegImage, "image/jpeg");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{listing.Id}/photos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingPhotosResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items.Should().ContainSingle().Which.Id.Should().Be(own.Id);

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task Given_UnknownListing_When_GettingListingPhotos_Then_Returns404()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = PublishedListing(seller, "Manual POO");
        await SeedAsync(seller, listing);
        await UploadAsSellerAsync(listing, PngImage, "image/png");
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}/photos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Photo>().CountAsync());
        count.Should().Be(1);
    }

    private static Listing PublishedListing(Member seller, string title)
    {
        var listing = Listing.Create(seller.Id, title, Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
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
