using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.GetPhotoFile;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.GetPhotoFile;

public class GetPhotoFileHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] ThumbnailBytes = [9, 9];

    private static Photo BuildPhoto()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        return Photo.Create(listing, new ProcessedPhoto(PngBytes, ThumbnailBytes), Now);
    }

    [Fact]
    public async Task Given_PhotoWithFileInStorage_When_Handling_Then_ReturnsBytesAndContentType()
    {
        var photo = BuildPhoto();
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        var storage = new FakePhotoStorage();
        storage.Files[(photo.ListingId, photo.FileName)] = PngBytes;
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photo.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/webp");
        using var reader = new MemoryStream();
        await result.Value.Content.CopyToAsync(reader);
        reader.ToArray().Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Given_PhotoWithoutFileInStorage_When_Handling_Then_ReturnsNotFound()
    {
        var photo = BuildPhoto();
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        var storage = new FakePhotoStorage();
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photo.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Photo.NotFound");
        result.Error.Message.Should().Be($"Photo '{photo.Id}' was not found.");
        photos.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_UnknownPhotoId_When_Handling_Then_ReturnsNotFound()
    {
        var photoId = Guid.NewGuid();
        var photos = new FakePhotoRepository();
        var storage = new FakePhotoStorage();
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photoId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Photo.NotFound");
        result.Error.Message.Should().Be($"Photo '{photoId}' was not found.");
    }

    private static readonly byte[] SvgBytes = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();

    private static Photo BuildGeneratedPhoto()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return Photo.CreateGenerated(listing, 0, SvgBytes, PhotoFormat.Svg, Now).Value;
    }

    private static async Task<byte[]> ReadAll(Stream stream)
    {
        using var reader = new MemoryStream();
        await stream.CopyToAsync(reader);
        return reader.ToArray();
    }

    [Fact]
    public async Task Given_PhotoWithThumbnail_When_ThumbnailRequested_Then_ReturnsThumbnailBytes()
    {
        var photo = BuildPhoto();
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        var storage = new FakePhotoStorage();
        storage.Files[(photo.ListingId, photo.FileName)] = PngBytes;
        storage.Files[(photo.ListingId, photo.ThumbnailFileName!)] = ThumbnailBytes;
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photo.Id, Thumbnail: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/webp");
        (await ReadAll(result.Value.Content)).Should().Equal(ThumbnailBytes);
    }

    [Fact]
    public async Task Given_GeneratedPhotoWithoutThumbnail_When_ThumbnailRequested_Then_ReturnsImage()
    {
        var photo = BuildGeneratedPhoto();
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        var storage = new FakePhotoStorage();
        storage.Files[(photo.ListingId, photo.FileName)] = SvgBytes;
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photo.Id, Thumbnail: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be(PhotoFormat.Svg.ContentType);
        (await ReadAll(result.Value.Content)).Should().Equal(SvgBytes);
    }

    [Fact]
    public async Task Given_PhotoWithThumbnail_When_ThumbnailNotRequested_Then_ReturnsImage()
    {
        var photo = BuildPhoto();
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        var storage = new FakePhotoStorage();
        storage.Files[(photo.ListingId, photo.FileName)] = PngBytes;
        storage.Files[(photo.ListingId, photo.ThumbnailFileName!)] = ThumbnailBytes;
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photo.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/webp");
        (await ReadAll(result.Value.Content)).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Given_ThumbnailFileMissing_When_ThumbnailRequested_Then_ReturnsNotFound()
    {
        var photo = BuildPhoto();
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        var storage = new FakePhotoStorage();
        storage.Files[(photo.ListingId, photo.FileName)] = PngBytes;
        var handler = new GetPhotoFileHandler(photos, storage);

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photo.Id, Thumbnail: true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Photo.NotFound");
        result.Error.Message.Should().Be($"Photo '{photo.Id}' was not found.");
    }

    [Fact]
    public async Task Given_UnknownPhotoId_When_ThumbnailRequested_Then_ReturnsNotFound()
    {
        var photoId = Guid.NewGuid();
        var handler = new GetPhotoFileHandler(new FakePhotoRepository(), new FakePhotoStorage());

        var result = await handler.HandleAsync(new GetPhotoFileQuery(photoId, Thumbnail: true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Photo.NotFound");
        result.Error.Message.Should().Be($"Photo '{photoId}' was not found.");
    }
}
