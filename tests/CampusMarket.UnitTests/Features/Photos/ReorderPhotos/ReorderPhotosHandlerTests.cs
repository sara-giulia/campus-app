using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Features.Photos.ReorderPhotos;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Photos.ReorderPhotos;

public class ReorderPhotosHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] ThumbnailBytes = [9, 9];

    private sealed record Scenario(
        ReorderPhotosHandler Handler,
        FakePhotoRepository Photos,
        FakeUnitOfWork UnitOfWork,
        Listing Listing,
        List<Photo> Seeded);

    // Photos at positions 0, 1, 2...
    private static Scenario BuildScenario(bool publish, int photoCount = 3)
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        if (publish)
            listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var photos = new FakePhotoRepository();
        var seeded = new List<Photo>();
        for (var i = 0; i < photoCount; i++)
            seeded.Add(Photo.Create(listing, new ProcessedPhoto(PngBytes, ThumbnailBytes), Now.AddMinutes(i), i));
        photos.Items.AddRange(seeded);
        var unitOfWork = new FakeUnitOfWork();
        return new Scenario(new ReorderPhotosHandler(listings, photos, unitOfWork), photos, unitOfWork, listing, seeded);
    }

    private static List<Guid> Ids(Scenario s) => s.Seeded.Select(p => p.Id).ToList();

    private static void AssertNothingSaved(Scenario s)
    {
        s.Photos.UpdateCount.Should().Be(0);
        s.UnitOfWork.SaveCount.Should().Be(0);
        s.Seeded.Select(p => p.Position).Should().Equal(Enumerable.Range(0, s.Seeded.Count));
    }

    [Fact]
    public async Task Given_ThreePhotosAndReversedIds_When_SellerReorders_Then_UpdatesPositionsAndReturnsNewOrder()
    {
        var s = BuildScenario(publish: true);
        var reversed = Ids(s).AsEnumerable().Reverse().ToList();

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, reversed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(3);
        result.Value.Items.Select(i => i.Id).Should().Equal(reversed);
        result.Value.Items.Select(i => i.Position).Should().Equal(0, 1, 2);
        s.Seeded[2].Position.Should().Be(0);
        s.Seeded[1].Position.Should().Be(1);
        s.Seeded[0].Position.Should().Be(2);
        s.Photos.UpdateCount.Should().Be(3);
        s.UnitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_Draft_When_SellerReorders_Then_Succeeds()
    {
        var s = BuildScenario(publish: false);
        var reversed = Ids(s).AsEnumerable().Reverse().ToList();

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, reversed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(reversed);
        s.UnitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Reordering_Then_ReturnsNotFoundAndSavesNothing()
    {
        var s = BuildScenario(publish: true);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, Guid.NewGuid(), Ids(s)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Match("Listing '*' was not found.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_DraftOfAnotherMember_When_Reordering_Then_ReturnsNotFoundAndSavesNothing()
    {
        var s = BuildScenario(publish: false);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(OtherMemberId, false, s.Listing.Id, Ids(s)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{s.Listing.Id}' was not found.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_ActiveListing_When_OtherMemberReorders_Then_ReturnsNotSellerAndSavesNothing()
    {
        var s = BuildScenario(publish: true);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(OtherMemberId, false, s.Listing.Id, Ids(s)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_ActiveListing_When_AdminWhoIsNotSellerReorders_Then_ReturnsNotSellerAndSavesNothing()
    {
        var s = BuildScenario(publish: true);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(AdminId, true, s.Listing.Id, Ids(s)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Photo.NotSeller");
        result.Error.Message.Should().Be("Only the seller can change the photos of this listing.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_ListWithAMissingId_When_Reordering_Then_ReturnsOrderMismatchAndSavesNothing()
    {
        var s = BuildScenario(publish: true);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, Ids(s).Take(2).ToList()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.OrderMismatch");
        result.Error.Message.Should().Be("The list must contain every photo of the listing exactly once.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_ListWithAnUnknownId_When_Reordering_Then_ReturnsOrderMismatchAndSavesNothing()
    {
        var s = BuildScenario(publish: true);
        var ids = new List<Guid> { s.Seeded[0].Id, s.Seeded[1].Id, Guid.NewGuid() };

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, ids), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.OrderMismatch");
        result.Error.Message.Should().Be("The list must contain every photo of the listing exactly once.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_ListWithARepeatedId_When_Reordering_Then_ReturnsOrderMismatchAndSavesNothing()
    {
        var s = BuildScenario(publish: true);
        var ids = new List<Guid> { s.Seeded[0].Id, s.Seeded[1].Id, s.Seeded[1].Id };

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, ids), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.OrderMismatch");
        result.Error.Message.Should().Be("The list must contain every photo of the listing exactly once.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_EmptyListAndListingWithPhotos_When_Reordering_Then_ReturnsOrderMismatchAndSavesNothing()
    {
        var s = BuildScenario(publish: true);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, []), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.OrderMismatch");
        result.Error.Message.Should().Be("The list must contain every photo of the listing exactly once.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_AllPhotosPlusAnUnknownId_When_Reordering_Then_ReturnsOrderMismatchAndSavesNothing()
    {
        var s = BuildScenario(publish: true);
        var ids = new List<Guid> { s.Seeded[0].Id, s.Seeded[1].Id, s.Seeded[2].Id, Guid.NewGuid() };

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, ids), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Photo.OrderMismatch");
        result.Error.Message.Should().Be("The list must contain every photo of the listing exactly once.");
        AssertNothingSaved(s);
    }

    [Fact]
    public async Task Given_EmptyListAndListingWithoutPhotos_When_Reordering_Then_SucceedsWithEmptyItems()
    {
        var s = BuildScenario(publish: true, photoCount: 0);

        var result = await s.Handler.HandleAsync(new ReorderPhotosCommand(SellerId, false, s.Listing.Id, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        s.Photos.UpdateCount.Should().Be(0);
    }
}
