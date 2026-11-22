using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.UploadPhoto;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.UploadPhoto;

public class UploadPhotoHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] GifBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static Listing BuildListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static UploadPhotoHandler BuildHandler(
        FakeListingRepository listings, FakePhotoRepository photos, FakePhotoStorage storage, FakeUnitOfWork unitOfWork) =>
        new(listings, photos, storage, unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_SellerWithPng_When_Handling_Then_SavesFileAndPhotoAndCommitsOnce()
    {
        var listing = BuildListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listing.Id, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.Url.Should().Be($"/api/v1/photos/{result.Value.Id}");
        result.Value.ContentType.Should().Be("image/png");
        result.Value.SizeBytes.Should().Be(PngBytes.Length);
        result.Value.CreatedAt.Should().Be(Now);
        photos.Items.Should().ContainSingle();
        photos.Items[0].Id.Should().Be(result.Value.Id);
        storage.Files.Should().ContainSingle();
        storage.Files[(listing.Id, photos.Items[0].FileName)].Should().Equal(PngBytes);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithFivePhotos_When_Handling_Then_ReturnsLimitReachedAndSavesNothing()
    {
        var listing = BuildListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        photos.Items.Add(Photo.Create(SellerId, listing, 0, PngBytes, Now).Value);
        photos.Items.Add(Photo.Create(SellerId, listing, 1, PngBytes, Now).Value);
        photos.Items.Add(Photo.Create(SellerId, listing, 2, PngBytes, Now).Value);
        photos.Items.Add(Photo.Create(SellerId, listing, 3, PngBytes, Now).Value);
        photos.Items.Add(Photo.Create(SellerId, listing, 4, PngBytes, Now).Value);
        var existing = photos.Items.ToList();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listing.Id, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.LimitReached");
        result.Error.Message.Should().Be($"A listing can have at most {Photo.MaxPerListing} photos.");
        photos.Items.Should().Equal(existing);
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFoundAndSavesNothing()
    {
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listingId, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMember_When_Handling_Then_ReturnsForbiddenAndSavesNothing()
    {
        var listing = BuildListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(OtherMemberId, listing.Id, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Gif_When_Handling_Then_ReturnsInvalidFormatAndSavesNothing()
    {
        var listing = BuildListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listing.Id, GifBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.InvalidFormat");
        result.Error.Message.Should().Be("Only JPEG, PNG and WebP images are allowed.");
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
