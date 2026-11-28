using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.Api.Features.Photos.ReorderPhotos;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Photos;

public sealed class ReorderPhotosTests : IntegrationTestBase
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid OtherMemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Given_ThreePhotos_When_SellerReversesThem_Then_Returns200AndSavesNewPositions()
    {
        // Arrange
        var (seller, listing, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(seller.Id);
        var reversed = photos.AsEnumerable().Reverse().Select(p => p.Id).ToList();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(reversed), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingPhotosResponse>(Json);
        body!.Count.Should().Be(3);
        body.Items.Select(p => p.Id).Should().Equal(reversed);
        body.Items.Select(p => p.Position).Should().Equal(0, 1, 2);

        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().ToListAsync());
        saved.Single(p => p.Id == reversed[0]).Position.Should().Be(0);
        saved.Single(p => p.Id == reversed[1]).Position.Should().Be(1);
        saved.Single(p => p.Id == reversed[2]).Position.Should().Be(2);

        var listed = await Client.GetFromJsonAsync<GetListingPhotosResponse>($"/api/v1/listings/{listing.Id}/photos", Json);
        listed!.Items.Select(p => p.Id).Should().Equal(reversed);
    }

    [Fact]
    public async Task Given_NoToken_When_ReorderingPhotos_Then_Returns401AndKeepsPositions()
    {
        // Arrange
        var (_, listing, photos) = await SeedPhotosAsync(published: true);
        var ids = photos.AsEnumerable().Reverse().Select(p => p.Id).ToList();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/photos/order");

        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_UnknownListing_When_ReorderingPhotos_Then_Returns404()
    {
        // Arrange
        var (seller, _, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(seller.Id);
        var ids = photos.Select(p => p.Id).ToList();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{Guid.NewGuid()}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_DraftOfAnotherMember_When_ReorderingPhotos_Then_Returns404()
    {
        // Arrange
        var (_, listing, photos) = await SeedPhotosAsync(published: false);
        AuthenticateAs(OtherMemberId);
        var ids = photos.AsEnumerable().Reverse().Select(p => p.Id).ToList();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_ActiveListing_When_OtherMemberReordersPhotos_Then_Returns403NotSeller()
    {
        // Arrange
        var (_, listing, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(OtherMemberId);
        var ids = photos.AsEnumerable().Reverse().Select(p => p.Id).ToList();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.NotSeller");
        error.Type.Should().Be(ErrorType.Forbidden);

        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_MissingId_When_ReorderingPhotos_Then_Returns400OrderMismatchAndKeepsPositions()
    {
        // Arrange
        var (seller, listing, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(seller.Id);
        var ids = photos.Take(2).Select(p => p.Id).ToList();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        await AssertOrderMismatchAsync(response);
        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_UnknownId_When_ReorderingPhotos_Then_Returns400OrderMismatchAndKeepsPositions()
    {
        // Arrange
        var (seller, listing, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(seller.Id);
        var ids = new List<Guid> { photos[0].Id, photos[1].Id, Guid.NewGuid() };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        await AssertOrderMismatchAsync(response);
        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_RepeatedId_When_ReorderingPhotos_Then_Returns400OrderMismatchAndKeepsPositions()
    {
        // Arrange
        var (seller, listing, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(seller.Id);
        var ids = new List<Guid> { photos[0].Id, photos[1].Id, photos[1].Id };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new ReorderPhotosRequest(ids), Json);

        // Assert
        await AssertOrderMismatchAsync(response);
        await AssertPositionsUnchangedAsync(photos);
    }

    [Fact]
    public async Task Given_EmptyBodyAndListingWithPhotos_When_ReorderingPhotos_Then_Returns400OrderMismatchAndKeepsPositions()
    {
        // Arrange
        var (seller, listing, photos) = await SeedPhotosAsync(published: true);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}/photos/order", new { }, Json);

        // Assert
        await AssertOrderMismatchAsync(response);
        await AssertPositionsUnchangedAsync(photos);
    }

    // A seller with a listing and three photos at positions 0, 1 and 2, saved straight into the database.
    private async Task<(Member Seller, Listing Listing, List<Photo> Photos)> SeedPhotosAsync(bool published)
    {
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        if (published)
            listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var photos = Enumerable.Range(0, 3)
            .Select(i => Photo.Create(listing, new ProcessedPhoto([1], [1]), CreatedAt.AddMinutes(i), i))
            .ToList();

        await SeedAsync(seller, listing);
        await SeedAsync(photos.Cast<object>().ToArray());
        return (seller, listing, photos);
    }

    private static async Task AssertOrderMismatchAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Photo.OrderMismatch");
        error.Type.Should().Be(ErrorType.Validation);
    }

    private async Task AssertPositionsUnchangedAsync(List<Photo> seeded)
    {
        var saved = await QueryDbAsync(db => db.Set<Photo>().AsNoTracking().ToListAsync());
        saved.Should().HaveCount(seeded.Count);
        foreach (var photo in seeded)
            saved.Single(p => p.Id == photo.Id).Position.Should().Be(photo.Position);
    }
}
