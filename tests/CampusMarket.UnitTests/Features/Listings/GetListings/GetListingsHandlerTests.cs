using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetListings;

public class GetListingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_EmptyRepository_When_Handling_Then_ReturnsEmptyList()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_RepositoryWithTwoListings_When_Handling_Then_ReturnsThemAndLeavesRepositoryUnchanged()
    {
        var first = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        var second = Listing.Create(Guid.NewGuid(), "Lab coat", Category.LabEquipment, Condition.New, 60m, Now).Value;
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        repository.Items.Should().HaveCount(2);
        repository.Items.Should().ContainKey(first.Id);
        repository.Items.Should().ContainKey(second.Id);
    }
}
