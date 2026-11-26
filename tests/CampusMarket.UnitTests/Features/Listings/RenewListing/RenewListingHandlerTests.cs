using CampusMarket.Api.Common.Listings;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.RenewListing;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Features.Listings.RenewListing;

public class RenewListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");
    private static readonly IOptions<ListingOptions> ListingOptions = Options.Create(new ListingOptions { ExpiryDays = 30 });

    private readonly FakeListingRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCategoryRepository _categories = new();

    private RenewListingHandler CreateHandler() =>
        new(_repository, _unitOfWork, _categories, new FixedTimeProvider(Now), ListingOptions);

    private Listing AddExpiredListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now.AddDays(-60)).Value;
        listing.Publish(Now.AddDays(-30));
        listing.Expire(Now.AddDays(-30)).Should().BeTrue();
        _repository.Items[listing.Id] = listing;
        return listing;
    }

    private Listing AddActiveListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(10));
        _repository.Items[listing.Id] = listing;
        return listing;
    }

    private void AssertNothingSaved()
    {
        _repository.UpdateCount.Should().Be(0);
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExpiredListing_When_SellerRenews_Then_ListingIsActiveWithNewExpiresAtAndSavedOnce()
    {
        var listing = AddExpiredListing();
        var versionBefore = listing.Version;

        var result = await CreateHandler().HandleAsync(new RenewListingCommand(listing.Id, SellerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(listing.Id);
        listing.Status.Should().Be(ListingStatus.Active);
        listing.ExpiresAt.Should().Be(Now.AddDays(30));
        listing.Version.Should().Be(versionBefore + 1);
        _repository.UpdateCount.Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownId_When_Renewing_Then_ReturnsNotFoundAndSavesNothing()
    {
        var id = Guid.NewGuid();

        var result = await CreateHandler().HandleAsync(new RenewListingCommand(id, SellerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{id}' was not found.");
        AssertNothingSaved();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ExpiredListing_When_NotTheSellerRenews_Then_ReturnsNotOwnerAndSavesNothing(bool isAdmin)
    {
        var listing = AddExpiredListing();
        var requester = isAdmin ? AdminId : OtherMemberId;
        var versionBefore = listing.Version;

        var result = await CreateHandler().HandleAsync(new RenewListingCommand(listing.Id, requester), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller can renew this listing.");
        listing.Status.Should().Be(ListingStatus.Expired);
        listing.Version.Should().Be(versionBefore);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_ActiveListing_When_SellerRenews_Then_ReturnsNotExpiredAndSavesNothing()
    {
        var listing = AddActiveListing();
        var expiresAtBefore = listing.ExpiresAt;
        var versionBefore = listing.Version;

        var result = await CreateHandler().HandleAsync(new RenewListingCommand(listing.Id, SellerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotExpired");
        result.Error.Message.Should().Be("Only an expired listing can be renewed.");
        listing.Status.Should().Be(ListingStatus.Active);
        listing.ExpiresAt.Should().Be(expiresAtBefore);
        listing.Version.Should().Be(versionBefore);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_ExpiredListingInDeactivatedCategory_When_SellerRenews_Then_ReturnsCategoryErrorAndListingStaysExpired()
    {
        var listing = AddExpiredListing();
        _categories.Deactivate(Category.Textbook);
        var versionBefore = listing.Version;

        var result = await CreateHandler().HandleAsync(new RenewListingCommand(listing.Id, SellerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInactive");
        result.Error.Message.Should().Be("The category 'Textbook' is not active, a listing cannot be put in it.");
        listing.Status.Should().Be(ListingStatus.Expired);
        listing.Version.Should().Be(versionBefore);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Given_ExpiredListingWhoseCategoryIsNotFound_When_SellerRenews_Then_Renews()
    {
        // Arrange
        var listing = AddExpiredListing();
        _categories.Items.Remove(Category.Textbook);

        // Act
        var result = await CreateHandler().HandleAsync(new RenewListingCommand(listing.Id, SellerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        _repository.UpdateCount.Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ActiveListingInDeactivatedCategory_When_SellerRenews_Then_ReturnsNotExpiredNotTheCategoryError()
    {
        var listing = AddActiveListing();
        _categories.Deactivate(Category.Textbook);

        var result = await CreateHandler().HandleAsync(new RenewListingCommand(listing.Id, SellerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotExpired");
        result.Error.Message.Should().Be("Only an expired listing can be renewed.");
        listing.Status.Should().Be(ListingStatus.Active);
        AssertNothingSaved();
    }
}
