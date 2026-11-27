using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.DeletePhoto;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.DeletePhoto;

public class DeletePhotoHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] ThumbnailBytes = [9, 9];

    private static Listing BuildListing(string title) =>
        Listing.Create(SellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Photo AddPhoto(Listing listing, FakePhotoRepository photos, FakePhotoStorage storage)
    {
        var photo = Photo.Create(listing, new ProcessedPhoto(PngBytes, ThumbnailBytes), Now);
        photos.Items.Add(photo);
        storage.Files[(listing.Id, photo.FileName)] = PngBytes;
        return photo;
    }

    [Fact]
    public async Task Given_Seller_When_Handling_Then_RemovesPhotoAndFileAndCommitsOnce()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var photo = AddPhoto(listing, photos, storage);
        var other = AddPhoto(listing, photos, storage);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new DeletePhotoCommand(SellerId, listing.Id, photo.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(photo.Id);
        photos.Items.Should().Equal(other);
        storage.Files.Keys.Should().Equal((listing.Id, other.FileName));
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnotherMember_When_Handling_Then_ReturnsForbiddenAndRemovesNothing()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var photo = AddPhoto(listing, photos, storage);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new DeletePhotoCommand(OtherMemberId, listing.Id, photo.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
        photos.Items.Should().Equal(photo);
        storage.Files.Should().ContainKey((listing.Id, photo.FileName));
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFoundAndRemovesNothing()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var photo = AddPhoto(listing, photos, storage);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new DeletePhotoCommand(SellerId, listing.Id, photo.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listing.Id}' was not found.");
        photos.Items.Should().Equal(photo);
        storage.Files.Should().ContainKey((listing.Id, photo.FileName));
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownPhoto_When_Handling_Then_ReturnsNotFoundAndRemovesNothing()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var photo = AddPhoto(listing, photos, storage);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);
        var unknownId = Guid.NewGuid();

        var result = await handler.HandleAsync(new DeletePhotoCommand(SellerId, listing.Id, unknownId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Photo.NotFound");
        result.Error.Message.Should().Be($"Photo '{unknownId}' was not found.");
        photos.Items.Should().Equal(photo);
        storage.Files.Should().ContainKey((listing.Id, photo.FileName));
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PhotoOfAnotherListingOfSameSeller_When_Handling_Then_ReturnsNotFoundAndRemovesNothing()
    {
        var listing = BuildListing("Algebra book");
        var otherListing = BuildListing("Physics notes");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        listings.Items[otherListing.Id] = otherListing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var photo = AddPhoto(otherListing, photos, storage);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new DeletePhotoCommand(SellerId, listing.Id, photo.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Photo.NotFound");
        result.Error.Message.Should().Be($"Photo '{photo.Id}' was not found.");
        photos.Items.Should().Equal(photo);
        storage.Files.Should().ContainKey((otherListing.Id, photo.FileName));
        unitOfWork.SaveCount.Should().Be(0);
    }

    private static readonly byte[] SvgBytes = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();

    [Fact]
    public async Task Given_ProcessedPhoto_When_SellerDeletes_Then_ImageAndThumbnailAreGone()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var photo = AddPhoto(listing, photos, storage);
        storage.Files[(listing.Id, photo.ThumbnailFileName!)] = ThumbnailBytes;
        var other = AddPhoto(listing, photos, storage);
        storage.Files[(listing.Id, other.ThumbnailFileName!)] = ThumbnailBytes;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new DeletePhotoCommand(SellerId, listing.Id, photo.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        storage.Files.Should().NotContainKey((listing.Id, photo.FileName));
        storage.Files.Should().NotContainKey((listing.Id, photo.ThumbnailFileName!));
        storage.Files.Keys.Should().BeEquivalentTo(new[] { (listing.Id, other.FileName), (listing.Id, other.ThumbnailFileName!) });
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_GeneratedPhoto_When_SellerDeletes_Then_OnlyItsFileIsGone()
    {
        var listing = BuildListing("Algebra book");
        listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var generated = Photo.CreateGenerated(listing, 0, SvgBytes, PhotoFormat.Svg, Now).Value;
        var photos = new FakePhotoRepository();
        photos.Items.Add(generated);
        var storage = new FakePhotoStorage();
        storage.Files[(listing.Id, generated.FileName)] = SvgBytes;
        storage.Files[(listing.Id, "unrelated.webp")] = PngBytes;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeletePhotoHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new DeletePhotoCommand(SellerId, listing.Id, generated.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generated.Id);
        photos.Items.Should().BeEmpty();
        storage.Files.Keys.Should().Equal((listing.Id, "unrelated.webp"));
        storage.Files[(listing.Id, "unrelated.webp")].Should().Equal(PngBytes);
        unitOfWork.SaveCount.Should().Be(1);
    }
}
