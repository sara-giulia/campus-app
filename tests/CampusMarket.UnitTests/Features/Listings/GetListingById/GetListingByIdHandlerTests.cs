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
        var query = new GetListingByIdQuery(listing.Id, listing.SellerId);

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
        var unknownId = Guid.NewGuid();
        var query = new GetListingByIdQuery(unknownId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownId}' was not found.");
    }

    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    private static (FakeListingRepository Repository, Listing Listing) SeedListing(bool publish)
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        if (publish)
            listing.Publish();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        return (repository, listing);
    }

    [Fact]
    public async Task Given_Draft_When_SellerAsks_Then_ReturnsIt()
    {
        var (repository, listing) = SeedListing(publish: false);
        var handler = new GetListingByIdHandler(repository);

        var result = await handler.HandleAsync(new GetListingByIdQuery(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(listing.Id);
        result.Value.Status.Should().Be(ListingStatus.Draft);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Draft_When_AdminAsks_Then_ReturnsIt()
    {
        var (repository, listing) = SeedListing(publish: false);
        var handler = new GetListingByIdHandler(repository);

        var result = await handler.HandleAsync(new GetListingByIdQuery(listing.Id, AdminId, true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(listing.Id);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Draft_When_OtherMemberAsks_Then_ReturnsNotFound()
    {
        var (repository, listing) = SeedListing(publish: false);
        var handler = new GetListingByIdHandler(repository);

        var result = await handler.HandleAsync(new GetListingByIdQuery(listing.Id, OtherMemberId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listing.Id}' was not found.");
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Draft_When_AnonymousAsks_Then_ReturnsNotFound()
    {
        var (repository, listing) = SeedListing(publish: false);
        var handler = new GetListingByIdHandler(repository);

        var result = await handler.HandleAsync(new GetListingByIdQuery(listing.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveListing_When_AnonymousAsks_Then_ReturnsIt()
    {
        var (repository, listing) = SeedListing(publish: true);
        var handler = new GetListingByIdHandler(repository);

        var result = await handler.HandleAsync(new GetListingByIdQuery(listing.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(listing.Id);
        result.Value.Status.Should().Be(ListingStatus.Active);
        repository.UpdateCount.Should().Be(0);
    }
}
