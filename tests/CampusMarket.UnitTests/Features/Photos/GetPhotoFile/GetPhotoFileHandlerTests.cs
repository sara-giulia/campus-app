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

    private static Photo BuildPhoto()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        return Photo.Create(SellerId, listing, 0, PngBytes, Now).Value;
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
        result.Value.ContentType.Should().Be("image/png");
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
}
