using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.UpdateListing;

public class UpdateListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Listing BuildListing()
    {
        return Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    [Fact]
    public async Task Given_ExistingListing_When_HandlingValidCommand_Then_UpdatesEntityAndSavesOnce()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork);
        var command = new UpdateListingCommand(listing.Id, "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Title.Should().Be("Linear algebra");
        listing.Condition.Should().Be(Condition.New);
        listing.Price.Amount.Should().Be(30m);
        listing.Version.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork);
        var command = new UpdateListingCommand(Guid.NewGuid(), "Linear algebra", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExistingListing_When_HandlingWithInvalidData_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var listing = BuildListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateListingHandler(repository, unitOfWork);
        var command = new UpdateListingCommand(listing.Id, " ", Category.Textbook, Condition.New, 30m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleEmpty");
        unitOfWork.SaveCount.Should().Be(0);
        listing.Title.Should().Be("Algebra book");
        listing.Version.Should().Be(1);
    }
}
