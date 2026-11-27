using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.GetListingPhotos;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.GetListingPhotos;

public class GetListingPhotosHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ViewerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly FakePhotoRepository _photos = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _listing;
    private readonly Photo _photo;
    private readonly GetListingPhotosHandler _handler;

    public GetListingPhotosHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_seller.Id] = _seller;
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        _photo = Photo.Create(_seller.Id, _listing, 0, PngBytes, Now).Value;
        _photos.Items.Add(_photo);
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new GetListingPhotosHandler(listings, _photos, members, _clock);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_MemberReadsPhotos_Then_ReturnsListingNotFound()
    {
        var result = await _handler.HandleAsync(new GetListingPhotosQuery(_listing.Id, ViewerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{_listing.Id}' was not found.");
        _photos.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_AdminReadsPhotos_Then_ReturnsThem()
    {
        var result = await _handler.HandleAsync(new GetListingPhotosQuery(_listing.Id, AdminId, IsAdmin: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(p => p.Id).Should().Equal(_photo.Id);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_MemberReadsPhotos_Then_ReturnsThem()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new GetListingPhotosQuery(_listing.Id, ViewerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(1);
    }
}
