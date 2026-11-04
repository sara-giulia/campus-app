using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.DeleteListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.DeleteListing;

public class DeleteListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildListing()
    {
        return Listing.Create(OwnerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    [Fact]
    public async Task Given_ExistingListing_When_Handling_Then_RemovesItAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteListingHandler(repository, unitOfWork);
        var command = new DeleteListingCommand(listing.Id, listing.SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteListingHandler(repository, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new DeleteListingCommand(unknownId, Guid.NewGuid(), false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingOfAnotherMember_When_NonAdminDeletes_Then_ReturnsForbiddenAndKeepsListing()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteListingHandler(repository, unitOfWork);
        var command = new DeleteListingCommand(listing.Id, OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller or an Admin can delete this listing.");
        repository.Items.Should().ContainKey(listing.Id);
        listing.Title.Should().Be("Algebra book");
        listing.Version.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingOfAnotherMember_When_AdminDeletes_Then_RemovesItAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteListingHandler(repository, unitOfWork);
        var command = new DeleteListingCommand(listing.Id, AdminId, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        repository.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_NonOwnerNonAdminDeletes_Then_ReturnsNotFoundNotForbidden()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteListingHandler(repository, unitOfWork);
        var unknownId = Guid.NewGuid();
        var command = new DeleteListingCommand(unknownId, OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }
}
