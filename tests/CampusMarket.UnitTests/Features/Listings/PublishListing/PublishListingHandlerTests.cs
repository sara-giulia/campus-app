using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.PublishListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.PublishListing;

public class PublishListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildDraftListing()
    {
        return Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    [Fact]
    public async Task Given_DraftListing_When_SellerPublishes_Then_ActivatesListingAndSavesOnce()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new PublishListingHandler(repository, unitOfWork);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DraftListing_When_AdminPublishes_Then_ActivatesListingAndSavesOnce()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new PublishListingHandler(repository, unitOfWork);
        var command = new PublishListingCommand(listing.Id, AdminId, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_DraftListing_When_NonOwnerNonAdminPublishes_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new PublishListingHandler(repository, unitOfWork);
        var command = new PublishListingCommand(listing.Id, OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        listing.Status.Should().Be(ListingStatus.Draft);
        listing.Version.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownId_When_Publishing_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new PublishListingHandler(repository, unitOfWork);
        var command = new PublishListingCommand(Guid.NewGuid(), SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveListing_When_SellerPublishes_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        listing.Publish();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new PublishListingHandler(repository, unitOfWork);
        var command = new PublishListingCommand(listing.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotDraft");
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
