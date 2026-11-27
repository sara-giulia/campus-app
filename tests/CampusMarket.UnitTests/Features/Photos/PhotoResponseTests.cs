using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos;

namespace CampusMarket.UnitTests.Features.Photos;

public class PhotoResponseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    private static readonly byte[] ImageBytes = [1, 2, 3, 4];
    private static readonly byte[] ThumbnailBytes = [9, 9];
    private static readonly byte[] SvgBytes = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();

    private static Listing BuildActiveListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public void Given_ProcessedPhoto_When_Mapping_Then_ThumbnailUrlPointsToThumbnail()
    {
        var photo = Photo.Create(BuildActiveListing(), new ProcessedPhoto(ImageBytes, ThumbnailBytes), Now, 2);

        var response = PhotoResponse.From(photo);

        response.Url.Should().Be($"/api/v1/photos/{photo.Id}");
        response.ThumbnailUrl.Should().Be($"/api/v1/photos/{photo.Id}/thumbnail");
        response.ContentType.Should().Be("image/webp");
        response.SizeBytes.Should().Be(4);
        response.Position.Should().Be(2);
        response.IsGenerated.Should().BeFalse();
    }

    [Fact]
    public void Given_GeneratedPhoto_When_Mapping_Then_ThumbnailUrlIsTheSameAsUrl()
    {
        var photo = Photo.CreateGenerated(BuildActiveListing(), 0, SvgBytes, PhotoFormat.Svg, Now).Value;

        var response = PhotoResponse.From(photo);

        response.Url.Should().Be($"/api/v1/photos/{photo.Id}");
        response.ThumbnailUrl.Should().Be(response.Url);
        response.IsGenerated.Should().BeTrue();
    }
}
