using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.GetListingPhotos;

public class GetListingPhotosHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static Listing BuildListing(string title) =>
        Listing.Create(SellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;

    private static readonly byte[] ThumbnailBytes = [9, 9];

    private static Photo BuildPhoto(Listing listing, byte[] content, DateTimeOffset createdAt, int position = 0) =>
        Photo.Create(listing, new ProcessedPhoto(content, ThumbnailBytes), createdAt, position);

    [Fact]
    public async Task Given_PhotosSeededOutOfOrder_When_Handling_Then_ReturnsThemByCreatedAtWithEveryField()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var newest = BuildPhoto(listing, PngBytes, Now.AddHours(2));
        var oldest = BuildPhoto(listing, JpegBytes, Now);
        var middle = BuildPhoto(listing, PngBytes, Now.AddHours(1));
        var photos = new FakePhotoRepository();
        photos.Items.AddRange([newest, oldest, middle]);
        var before = photos.Items.ToList();
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(3);
        result.Value.Items.Select(i => i.Id).Should().Equal(oldest.Id, middle.Id, newest.Id);
        result.Value.Items[0].ListingId.Should().Be(listing.Id);
        result.Value.Items[0].Url.Should().Be($"/api/v1/photos/{oldest.Id}");
        result.Value.Items[0].ContentType.Should().Be("image/webp");
        result.Value.Items[0].SizeBytes.Should().Be(JpegBytes.Length);
        result.Value.Items[0].CreatedAt.Should().Be(Now);
        photos.Items.Should().Equal(before);
        listings.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_PhotosWithSameCreatedAt_When_Handling_Then_OrdersThemById()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        photos.Items.Add(BuildPhoto(listing, PngBytes, Now));
        photos.Items.Add(BuildPhoto(listing, PngBytes, Now));
        photos.Items.Add(BuildPhoto(listing, PngBytes, Now));
        var before = photos.Items.ToList();
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.Items.Should().BeInAscendingOrder(i => i.Id);
        photos.Items.Should().Equal(before);
    }

    [Fact]
    public async Task Given_PhotosOfAnotherListing_When_Handling_Then_DoesNotReturnThem()
    {
        var listing = BuildListing("Algebra book");
        var otherListing = BuildListing("Physics notes");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        listings.Items[otherListing.Id] = otherListing;
        var own = BuildPhoto(listing, PngBytes, Now);
        var photos = new FakePhotoRepository();
        photos.Items.Add(own);
        photos.Items.Add(BuildPhoto(otherListing, PngBytes, Now));
        var before = photos.Items.ToList();
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(1);
        result.Value.Items.Select(i => i.Id).Should().Equal(own.Id);
        photos.Items.Should().Equal(before);
    }

    [Fact]
    public async Task Given_ListingWithNoPhoto_When_Handling_Then_ReturnsEmptyList()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        photos.Items.Should().BeEmpty();
        listings.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFound()
    {
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var photos = new FakePhotoRepository();
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listingId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        listings.Items.Should().BeEmpty();
        photos.Items.Should().BeEmpty();
    }

    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static (FakeListingRepository Listings, FakePhotoRepository Photos, Listing Listing, Photo Photo) SeedWithPhoto(bool publish)
    {
        var listing = BuildListing("Algebra book");
        if (publish)
            listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photo = BuildPhoto(listing, PngBytes, Now);
        var photos = new FakePhotoRepository();
        photos.Items.Add(photo);
        return (listings, photos, listing, photo);
    }

    [Fact]
    public async Task Given_PhotosWithDifferentPositions_When_Handling_Then_OrdersByPositionThenCreatedAt()
    {
        var listing = BuildListing("Algebra book");
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var last = BuildPhoto(listing, PngBytes, Now, position: 2);
        var tieNewer = BuildPhoto(listing, PngBytes, Now.AddHours(2), position: 1);
        var tieOlder = BuildPhoto(listing, PngBytes, Now.AddHours(1), position: 1);
        var cover = BuildPhoto(listing, PngBytes, Now.AddHours(3), position: 0);
        var photos = new FakePhotoRepository();
        photos.Items.AddRange([last, tieNewer, cover, tieOlder]);
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(cover.Id, tieOlder.Id, tieNewer.Id, last.Id);
        result.Value.Items.Select(i => i.Position).Should().Equal(0, 1, 1, 2);
    }

    [Fact]
    public async Task Given_Draft_When_SellerAsks_Then_ReturnsItsPhotos()
    {
        var (listings, photos, listing, photo) = SeedWithPhoto(publish: false);
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(photo.Id);
    }

    [Fact]
    public async Task Given_Draft_When_AdminAsks_Then_ReturnsItsPhotos()
    {
        var (listings, photos, listing, photo) = SeedWithPhoto(publish: false);
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, AdminId, true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(photo.Id);
    }

    [Fact]
    public async Task Given_Draft_When_OtherMemberAsks_Then_ReturnsNotFound()
    {
        var (listings, photos, listing, _) = SeedWithPhoto(publish: false);
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id, OtherMemberId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        photos.Items.Should().ContainSingle();
        photos.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Draft_When_AnonymousAsks_Then_ReturnsNotFound()
    {
        var (listings, photos, listing, _) = SeedWithPhoto(publish: false);
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        photos.Items.Should().ContainSingle();
        photos.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveListing_When_AnonymousAsks_Then_ReturnsItsPhotos()
    {
        var (listings, photos, listing, photo) = SeedWithPhoto(publish: true);
        var handler = new GetListingPhotosHandler(listings, photos, new FakeMemberRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetListingPhotosQuery(listing.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(photo.Id);
        photos.UpdateCount.Should().Be(0);
    }
}
