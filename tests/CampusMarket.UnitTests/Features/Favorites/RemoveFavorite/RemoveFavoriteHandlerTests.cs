using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Favorites.RemoveFavorite;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Favorites.RemoveFavorite;

public class RemoveFavoriteHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildPublishedListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_MemberFavorite_When_Removing_Then_DeletesItAndSavesOnce()
    {
        var listing = BuildPublishedListing();
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(MemberId, listing, Now.AddHours(-2)).Value);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RemoveFavoriteHandler(favorites, unitOfWork);
        var command = new RemoveFavoriteCommand(MemberId, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        favorites.Items.Should().BeEmpty();
        favorites.DeleteCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_NoFavoriteForListing_When_Removing_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var listingId = Guid.NewGuid();
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RemoveFavoriteHandler(favorites, unitOfWork);
        var command = new RemoveFavoriteCommand(MemberId, listingId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Favorite.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' is not in your favorites.");
        favorites.DeleteCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_OtherMemberFavoriteForSameListing_When_Removing_Then_ReturnsNotFoundAndKeepsIt()
    {
        var listing = BuildPublishedListing();
        var favorites = new FakeFavoriteRepository();
        var otherFavorite = Favorite.Create(OtherMemberId, listing, Now.AddHours(-2)).Value;
        favorites.Items.Add(otherFavorite);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RemoveFavoriteHandler(favorites, unitOfWork);
        var command = new RemoveFavoriteCommand(MemberId, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Favorite.NotFound");
        result.Error.Message.Should().Be($"Listing '{listing.Id}' is not in your favorites.");
        favorites.Items.Should().ContainSingle().Which.Should().BeSameAs(otherFavorite);
        favorites.DeleteCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }
}
