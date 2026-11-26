using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Moderation.HideListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.HideListing;

public class HideListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildPublishedListing()
    {
        var listing = BuildDraftListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_ActiveListing_When_Handling_Then_HidesListingAndSavesOnce()
    {
        var listing = BuildPublishedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, unitOfWork);
        var command = new HideListingCommand(listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Hidden);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new HideListingCommand(unknownId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_HiddenListing_When_Handling_Then_ReturnsAlreadyHiddenAndDoesNotSave()
    {
        var listing = BuildPublishedListing();
        listing.Hide();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, unitOfWork);
        var command = new HideListingCommand(listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.AlreadyHidden");
        result.Error.Message.Should().Be("The listing is already hidden.");
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_ReturnsCannotHideAndDoesNotSave()
    {
        var listing = BuildDraftListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new HideListingHandler(repository, unitOfWork);
        var command = new HideListingCommand(listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CannotHide");
        result.Error.Message.Should().Be("Only an active or reserved listing can be hidden.");
        listing.Status.Should().Be(ListingStatus.Draft);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
