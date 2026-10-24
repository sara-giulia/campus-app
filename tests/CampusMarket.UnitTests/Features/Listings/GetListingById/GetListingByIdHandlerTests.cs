using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetListingById;

public class GetListingByIdHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ExistingListing_When_Handling_Then_ReturnsResponseMatchingEntity()
    {
        var listing = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetListingByIdHandler(repository);
        var query = new GetListingByIdQuery(listing.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(listing.Id);
        result.Value.SellerId.Should().Be(listing.SellerId);
        result.Value.Title.Should().Be(listing.Title);
        result.Value.Category.Should().Be(listing.Category);
        result.Value.Condition.Should().Be(listing.Condition);
        result.Value.Price.Should().Be(listing.Price.Amount);
        result.Value.Status.Should().Be(listing.Status);
        result.Value.Version.Should().Be(listing.Version);
        result.Value.CreatedAt.Should().Be(listing.CreatedAt);
        repository.Items.Should().ContainKey(listing.Id);
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFound()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingByIdHandler(repository);
        var query = new GetListingByIdQuery(Guid.NewGuid());

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
    }
}
