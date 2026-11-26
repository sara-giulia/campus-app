using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Favorites.AddFavorite;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Favorites.AddFavorite;

public class AddFavoriteHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Member BuildMember() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Listing BuildPublishedListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static AddFavoriteHandler BuildHandler(
        FakeListingRepository listings,
        FakeMemberRepository members,
        FakeFavoriteRepository favorites,
        FakeUnitOfWork unitOfWork) =>
        new(listings, members, favorites, unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_PublishedListingAndExistingMember_When_Handling_Then_StoresFavoriteAndSavesOnce()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        favorites.Items.Should().ContainSingle();
        result.Value.Id.Should().Be(favorites.Items[0].Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.CreatedAt.Should().Be(Now);
        favorites.Items[0].MemberId.Should().Be(member.Id);
        favorites.Items[0].ListingId.Should().Be(listing.Id);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsListingNotFoundAndDoesNotSave()
    {
        var member = BuildMember();
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, listingId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        favorites.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_ReturnsMemberNotFoundAndDoesNotSave()
    {
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(OtherMemberId, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{OtherMemberId}' was not found.");
        favorites.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListingAndUnknownMember_When_Handling_Then_ReturnsListingNotFound()
    {
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(OtherMemberId, listingId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        favorites.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberIsSeller_When_Handling_Then_ReturnsOwnListingErrorAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(member.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorite.OwnListing");
        result.Error.Message.Should().Be("A seller cannot add their own listing to favorites.");
        favorites.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_ReturnsListingNotAvailableAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorite.ListingNotAvailable");
        result.Error.Message.Should().Be("Only a published listing can be added to favorites.");
        favorites.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberAlreadySavedListing_When_Handling_Then_ReturnsAlreadyExistsAndKeepsOneFavorite()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository();
        var existing = Favorite.Create(member.Id, listing, Now.AddHours(-2)).Value;
        favorites.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Favorite.AlreadyExists");
        result.Error.Message.Should().Be("This listing is already in your favorites.");
        favorites.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberSavedAnotherListing_When_Handling_Then_StoresSecondFavorite()
    {
        var member = BuildMember();
        var savedListing = BuildPublishedListing(SellerId);
        var newListing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[savedListing.Id] = savedListing;
        listings.Items[newListing.Id] = newListing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository();
        favorites.Items.Add(Favorite.Create(member.Id, savedListing, Now.AddHours(-2)).Value);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, newListing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(newListing.Id);
        favorites.Items.Should().HaveCount(2);
        favorites.Items.Select(f => f.ListingId).Should().BeEquivalentTo(new[] { savedListing.Id, newListing.Id });
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsDuplicateAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var favorites = new FakeFavoriteRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, favorites, unitOfWork);
        var command = new AddFavoriteCommand(member.Id, listing.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Favorite.Duplicate");
        result.Error.Message.Should().Match("A favorite with id '*' already exists.");
        favorites.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
