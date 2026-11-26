using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.GenerateListingImage;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.GenerateListingImage;

public class GenerateListingImageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now, "Second edition").Value;

    private static Listing BuildActiveListing()
    {
        var listing = BuildDraftListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static GenerateListingImageHandler BuildHandler(
        FakeListingRepository listings,
        FakePhotoRepository photos,
        FakeCategoryRepository categories,
        FakeImageGenerator generator,
        FakePhotoStorage storage,
        FakeUnitOfWork unitOfWork) =>
        new(listings, photos, categories, generator, storage, unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFoundAndDoesNotGenerate()
    {
        var listingId = Guid.NewGuid();
        var photos = new FakePhotoRepository();
        var generator = new FakeImageGenerator();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(new FakeListingRepository(), photos, new FakeCategoryRepository(), generator, storage, unitOfWork);

        var result = await handler.HandleAsync(new GenerateListingImageCommand(listingId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        generator.Prompts.Should().BeEmpty();
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_ReturnsListingNotActiveAndDoesNotGenerate()
    {
        var listing = BuildDraftListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var generator = new FakeImageGenerator();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, new FakeCategoryRepository(), generator, storage, unitOfWork);

        var result = await handler.HandleAsync(new GenerateListingImageCommand(listing.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.ListingNotActive");
        result.Error.Message.Should().Be("Only an active listing gets a generated image.");
        generator.Prompts.Should().BeEmpty();
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingWithPhoto_When_Handling_Then_ReturnsListingHasPhotosAndDoesNotGenerate()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var existing = Photo.Create(SellerId, listing, 0, PngBytes, Now).Value;
        photos.Items.Add(existing);
        var generator = new FakeImageGenerator();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, new FakeCategoryRepository(), generator, storage, unitOfWork);

        var result = await handler.HandleAsync(new GenerateListingImageCommand(listing.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Photo.ListingHasPhotos");
        result.Error.Message.Should().Be("The listing already has a photo.");
        generator.Prompts.Should().BeEmpty();
        photos.Items.Should().Equal(existing);
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveListingWithoutPhotos_When_Handling_Then_GeneratesSavesFileAndPhotoAndCommitsOnce()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var generator = new FakeImageGenerator();
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, new FakeCategoryRepository(), generator, storage, unitOfWork);

        var result = await handler.HandleAsync(new GenerateListingImageCommand(listing.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        generator.Prompts.Should().Equal(new ImagePrompt("Algebra book", "Second edition", Category.Textbook, "Manuale"));
        photos.Items.Should().ContainSingle();
        var photo = photos.Items[0];
        photo.IsGenerated.Should().BeTrue();
        photo.ListingId.Should().Be(listing.Id);
        photo.FileName.Should().Be($"{photo.Id}{PhotoFormat.Svg.Extension}");
        storage.Files.Should().ContainSingle();
        storage.Files[(listing.Id, photo.FileName)].Should().Equal(FakeImageGenerator.DefaultSvg);
        unitOfWork.SaveCount.Should().Be(1);
        result.Value.Id.Should().Be(photo.Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.Url.Should().Be($"/api/v1/photos/{photo.Id}");
        result.Value.ContentType.Should().Be(PhotoFormat.Svg.ContentType);
        result.Value.SizeBytes.Should().Be(FakeImageGenerator.DefaultSvg.Length);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.IsGenerated.Should().BeTrue();
    }

    [Fact]
    public async Task Given_NoCategoryRow_When_Handling_Then_PromptUsesEnumName()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var categories = new FakeCategoryRepository();
        categories.Items.Remove(Category.Textbook);
        var generator = new FakeImageGenerator();
        var handler = BuildHandler(
            listings, new FakePhotoRepository(), categories, generator, new FakePhotoStorage(), new FakeUnitOfWork());

        var result = await handler.HandleAsync(new GenerateListingImageCommand(listing.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        generator.Prompts.Should().ContainSingle();
        generator.Prompts[0].CategoryName.Should().Be(nameof(Category.Textbook));
    }

    [Fact]
    public async Task Given_GeneratorReturnsEmptyImage_When_Handling_Then_ReturnsEmptyAndSavesNothing()
    {
        var listing = BuildActiveListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var generator = new FakeImageGenerator { Image = new GeneratedImage(Array.Empty<byte>(), PhotoFormat.Svg) };
        var storage = new FakePhotoStorage();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, photos, new FakeCategoryRepository(), generator, storage, unitOfWork);

        var result = await handler.HandleAsync(new GenerateListingImageCommand(listing.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.Empty");
        result.Error.Message.Should().Be("No file was sent.");
        generator.Prompts.Should().ContainSingle();
        photos.Items.Should().BeEmpty();
        storage.Files.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
