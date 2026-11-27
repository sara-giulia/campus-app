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

    private static readonly byte[] SvgBytes = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();

    private static Listing BuildActiveListing()
    {
        var listing = BuildListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static Photo BuildGeneratedPhoto(Listing listing) =>
        Photo.CreateGenerated(listing, 0, SvgBytes, PhotoFormat.Svg, Now).Value;

    private static readonly byte[] ProcessedImage = [1, 2, 3, 4];
    private static readonly byte[] ProcessedThumbnail = [9, 9];
    private static readonly byte[] ThumbnailBytes = [9, 9];

    private static Photo BuildPhoto(Listing listing, int position = 0) =>
        Photo.Create(listing, new ProcessedPhoto(PngBytes, ThumbnailBytes), Now, position);

    private static UploadPhotoHandler BuildHandler(
        FakeListingRepository listings, FakePhotoRepository photos, FakePhotoStorage storage, FakeUnitOfWork unitOfWork) =>
        BuildHandler(listings, photos, new FakePhotoProcessor(), storage, unitOfWork);

    private static UploadPhotoHandler BuildHandler(
        FakeListingRepository listings, FakePhotoRepository photos, FakePhotoProcessor processor, FakePhotoStorage storage,
        FakeUnitOfWork unitOfWork) =>
        new(listings, photos, processor, storage, unitOfWork, new FixedTimeProvider(Now));

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
        result.Value.ContentType.Should().Be("image/webp");
        result.Value.SizeBytes.Should().Be(ProcessedImage.Length);
        result.Value.CreatedAt.Should().Be(Now);
        photos.Items.Should().ContainSingle();
        photos.Items[0].Id.Should().Be(result.Value.Id);
        storage.Files.Should().HaveCount(2);
        storage.Files[(listing.Id, photos.Items[0].FileName)].Should().Equal(ProcessedImage);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithFivePhotos_When_Handling_Then_ReturnsLimitReachedAndSavesNothing()
    {
        var listing = BuildListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
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

    [Fact]
    public async Task Given_ListingWithGeneratedPhoto_When_SellerUploadsPng_Then_ReplacesGeneratedPhotoAndItsFile()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var generated = BuildGeneratedPhoto(listing);
        var photos = new FakePhotoRepository();
        photos.Items.Add(generated);
        var storage = new FakePhotoStorage();
        storage.Files[(listing.Id, generated.FileName)] = SvgBytes;
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listing.Id, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsGenerated.Should().BeFalse();
        photos.Items.Should().ContainSingle();
        photos.Items[0].Id.Should().Be(result.Value.Id);
        storage.Files.Should().NotContainKey((listing.Id, generated.FileName));
        storage.Files.Should().HaveCount(2);
        storage.Files[(listing.Id, photos.Items[0].FileName)].Should().Equal(ProcessedImage);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_FiveRealPhotosAndOneGenerated_When_Handling_Then_ReturnsLimitReachedAndKeepsGeneratedPhoto()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var generated = BuildGeneratedPhoto(listing);
        var photos = new FakePhotoRepository();
        photos.Items.Add(generated);
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        var existing = photos.Items.ToList();
        var storage = new FakePhotoStorage();
        storage.Files[(listing.Id, generated.FileName)] = SvgBytes;
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listing.Id, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.LimitReached");
        result.Error.Message.Should().Be($"A listing can have at most {Photo.MaxPerListing} photos.");
        photos.Items.Should().Equal(existing);
        storage.Files.Should().ContainSingle();
        storage.Files[(listing.Id, generated.FileName)].Should().Equal(SvgBytes);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_FourRealPhotosAndOneGenerated_When_Handling_Then_SucceedsWithFiveRealPhotos()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var generated = BuildGeneratedPhoto(listing);
        var photos = new FakePhotoRepository();
        photos.Items.Add(generated);
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        photos.Items.Add(BuildPhoto(listing));
        var storage = new FakePhotoStorage();
        storage.Files[(listing.Id, generated.FileName)] = SvgBytes;
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);
        var command = new UploadPhotoCommand(SellerId, listing.Id, PngBytes);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        photos.Items.Should().HaveCount(Photo.MaxPerListing);
        photos.Items.Should().NotContain(generated);
        photos.Items.Should().OnlyContain(p => !p.IsGenerated);
        storage.Files.Should().NotContainKey((listing.Id, generated.FileName));
        unitOfWork.SaveCount.Should().Be(1);
    }

    private static (FakeListingRepository Listings, Listing Listing) SeedListing()
    {
        var listing = BuildListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        return (listings, listing);
    }

    [Fact]
    public async Task Given_ListingWithoutPhotos_When_Handling_Then_NewPhotoGetsPositionZero()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        var handler = BuildHandler(listings, photos, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be(0);
        photos.Items.Single().Position.Should().Be(0);
    }

    [Fact]
    public async Task Given_PhotosAtZeroAndOne_When_Handling_Then_NewPhotoGetsPositionTwo()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildPhoto(listing, 0));
        photos.Items.Add(BuildPhoto(listing, 1));
        var handler = BuildHandler(listings, photos, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be(2);
        photos.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_PhotosAtZeroAndFive_When_Handling_Then_NewPhotoGetsPositionSix()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildPhoto(listing, 0));
        photos.Items.Add(BuildPhoto(listing, 5));
        var handler = BuildHandler(listings, photos, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be(6);
    }

    [Fact]
    public async Task Given_OnlyAGeneratedPhoto_When_Handling_Then_NewPhotoGetsPositionZero()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildGeneratedPhoto(listing));
        var handler = BuildHandler(listings, photos, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be(0);
    }

    [Fact]
    public async Task Given_RealPhotoAtOneAndAGeneratedPhoto_When_Handling_Then_GeneratedPhotoIsIgnoredInPosition()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildGeneratedPhoto(listing));
        photos.Items.Add(BuildPhoto(listing, 1));
        var handler = BuildHandler(listings, photos, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Position.Should().Be(2);
    }

    [Fact]
    public async Task Given_StorageThrowsIOException_When_Handling_Then_ReturnsStorageFailedAndAddsNothing()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage { ThrowOnSave = new IOException("disk full") };
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.StorageFailed");
        result.Error.Message.Should().Be("The photo could not be saved. Try again later.");
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_StorageThrowsOperationCanceled_When_Handling_Then_ExceptionPropagatesAndNothingIsSaved()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage { ThrowOnSave = new OperationCanceledException() };
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, storage, unitOfWork);

        var act = () => handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        photos.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    private static readonly byte[] EmptyBytes = [];

    [Fact]
    public async Task Given_SellerWithPng_When_Handling_Then_ProcessorGetsUploadedBytesOnce()
    {
        var (listings, listing) = SeedListing();
        var processor = new FakePhotoProcessor();
        var handler = BuildHandler(listings, new FakePhotoRepository(), processor, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        processor.CallCount.Should().Be(1);
        processor.LastContent.Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Given_SellerWithPng_When_Handling_Then_SavesProcessedImageAndThumbnailAsWebP()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var handler = BuildHandler(listings, photos, new FakePhotoProcessor(), storage, new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var photo = photos.Items.Single();
        storage.Files.Should().HaveCount(2);
        storage.Files[(listing.Id, photo.FileName)].Should().Equal(ProcessedImage);
        storage.Files[(listing.Id, photo.ThumbnailFileName!)].Should().Equal(ProcessedThumbnail);
        storage.ContentTypes[(listing.Id, photo.FileName)].Should().Be("image/webp");
        storage.ContentTypes[(listing.Id, photo.ThumbnailFileName!)].Should().Be("image/webp");
    }

    [Fact]
    public async Task Given_SellerWithPng_When_Handling_Then_StoresRowWithThumbnailAndProcessedSize()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, new FakePhotoProcessor(), new FakePhotoStorage(), unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var photo = photos.Items.Single();
        photo.ThumbnailFileName.Should().Be($"{photo.Id}.thumb.webp");
        photo.SizeBytes.Should().Be(4);
        result.Value.ThumbnailUrl.Should().Be($"/api/v1/photos/{photo.Id}/thumbnail");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_OnlyAGeneratedPhoto_When_Handling_Then_RemovesItsRowAndFileAndNewPhotoIsAtZero()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var generated = BuildGeneratedPhoto(listing);
        var photos = new FakePhotoRepository();
        photos.Items.Add(generated);
        var storage = new FakePhotoStorage();
        storage.Files[(listing.Id, generated.FileName)] = SvgBytes;
        var handler = BuildHandler(listings, photos, new FakePhotoProcessor(), storage, new FakeUnitOfWork());

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        photos.Items.Should().NotContain(generated);
        storage.Files.Should().NotContainKey((listing.Id, generated.FileName));
        photos.Items.Single().Position.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_DoesNotCallProcessor()
    {
        var listingId = Guid.NewGuid();
        var processor = new FakePhotoProcessor();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(new FakeListingRepository(), new FakePhotoRepository(), processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listingId, PngBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        processor.CallCount.Should().Be(0);
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMember_When_Handling_Then_DoesNotCallProcessor()
    {
        var (listings, listing) = SeedListing();
        var processor = new FakePhotoProcessor();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(OtherMemberId, listing.Id, PngBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
        processor.CallCount.Should().Be(0);
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingWithFivePhotos_When_Handling_Then_DoesNotCallProcessor()
    {
        var (listings, listing) = SeedListing();
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildPhoto(listing, 0));
        photos.Items.Add(BuildPhoto(listing, 1));
        photos.Items.Add(BuildPhoto(listing, 2));
        photos.Items.Add(BuildPhoto(listing, 3));
        photos.Items.Add(BuildPhoto(listing, 4));
        var existing = photos.Items.ToList();
        var processor = new FakePhotoProcessor();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.LimitReached");
        result.Error.Message.Should().Be("A listing can have at most 5 photos.");
        processor.CallCount.Should().Be(0);
        photos.Items.Should().Equal(existing);
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyContent_When_Handling_Then_ReturnsEmptyAndDoesNotCallProcessor()
    {
        var (listings, listing) = SeedListing();
        var processor = new FakePhotoProcessor();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, EmptyBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.Empty");
        result.Error.Message.Should().Be("No file was sent.");
        processor.CallCount.Should().Be(0);
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ContentOverTenMegabytes_When_Handling_Then_ReturnsTooLargeAndDoesNotCallProcessor()
    {
        var (listings, listing) = SeedListing();
        var content = new byte[10_485_761];
        PngBytes.AsSpan(0, 8).CopyTo(content);
        var processor = new FakePhotoProcessor();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, content), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.TooLarge");
        result.Error.Message.Should().Be("A photo must be at most 10 MB.");
        processor.CallCount.Should().Be(0);
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Gif_When_Handling_Then_DoesNotCallProcessor()
    {
        var (listings, listing) = SeedListing();
        var processor = new FakePhotoProcessor();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, new FakePhotoRepository(), processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, GifBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.InvalidFormat");
        result.Error.Message.Should().Be("Only JPEG, PNG and WebP images are allowed.");
        processor.CallCount.Should().Be(0);
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Photo.InvalidFormat", "Only JPEG, PNG and WebP images are allowed.")]
    [InlineData("Photo.TooManyPixels", "A photo can have at most 40 megapixels.")]
    public async Task Given_ProcessorFails_When_Handling_Then_ReturnsItsErrorAndSavesNothing(string code, string message)
    {
        var (listings, listing) = SeedListing();
        var processor = new FakePhotoProcessor { Result = Result<ProcessedPhoto>.Failure(Error.Validation(code, message)) };
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, processor, storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be(code);
        result.Error.Message.Should().Be(message);
        processor.CallCount.Should().Be(1);
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_StorageThrowsWithGeneratedPhoto_When_Handling_Then_KeepsGeneratedPhotoAndDoesNotCommit()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var generated = BuildGeneratedPhoto(listing);
        var photos = new FakePhotoRepository();
        photos.Items.Add(generated);
        var storage = new FakePhotoStorage { ThrowOnSave = new IOException("disk full") };
        storage.Files[(listing.Id, generated.FileName)] = SvgBytes;
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, new FakePhotoProcessor(), storage, unitOfWork);

        var result = await handler.HandleAsync(new UploadPhotoCommand(SellerId, listing.Id, PngBytes), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.StorageFailed");
        result.Error.Message.Should().Be("The photo could not be saved. Try again later.");
        photos.Items.Should().Equal(generated);
        storage.Files.Keys.Should().Equal((listing.Id, generated.FileName));
        unitOfWork.SaveCount.Should().Be(0);
    }
}
