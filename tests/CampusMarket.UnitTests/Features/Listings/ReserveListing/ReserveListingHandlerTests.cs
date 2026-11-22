using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Listings.ReserveListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.ReserveListing;

public class ReserveListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherSellerId = new("44444444-4444-4444-4444-444444444444");

    private static Listing BuildActiveListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        return listing;
    }

    private static Member BuildBuyer()
    {
        return Member.Create("Buyer", "buyer@uni.edu", Now).Value;
    }

    [Fact]
    public async Task Given_ActiveListingAndExistingBuyer_When_Reserving_Then_ReservesListingAndSavesOnce()
    {
        var listing = BuildActiveListing(SellerId);
        var buyer = BuildBuyer();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(listing.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BuyerId.Should().Be(buyer.Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.Status.Should().Be(ReservationStatus.Active);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.ExpiresAt.Should().Be(Now.AddHours(48));
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Reservations.Should().HaveCount(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Reserving_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var unknownListingId = Guid.NewGuid();
        var command = new ReserveListingCommand(unknownListingId, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownListingId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownBuyer_When_Reserving_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var listing = BuildActiveListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var unknownBuyerId = Guid.NewGuid();
        var command = new ReserveListingCommand(listing.Id, unknownBuyerId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownBuyerId}' was not found.");
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Reserving_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        var buyer = BuildBuyer();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(listing.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotActive");
        result.Error.Message.Should().Be("Only an active listing can be reserved.");
        listing.Status.Should().Be(ListingStatus.Draft);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ActiveListing_When_SellerReservesOwnListing_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var seller = Member.Create("Seller", "seller@uni.edu", Now).Value;
        var listing = BuildActiveListing(seller.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(listing.Id, seller.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.OwnListing");
        result.Error.Message.Should().Be("A seller cannot reserve their own listing.");
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerWithThreeActiveReservations_When_Reserving_Then_ReturnsLimitReachedAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listingA = BuildActiveListing(OtherSellerId);
        listingA.Reserve(buyer.Id, Now);
        var listingB = BuildActiveListing(OtherSellerId);
        listingB.Reserve(buyer.Id, Now);
        var listingC = BuildActiveListing(OtherSellerId);
        listingC.Reserve(buyer.Id, Now);
        var target = BuildActiveListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listingA.Id] = listingA;
        listings.Items[listingB.Id] = listingB;
        listings.Items[listingC.Id] = listingC;
        listings.Items[target.Id] = target;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(target.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.LimitReached");
        result.Error.Message.Should().Be("A buyer can hold at most 3 active reservations.");
        target.Status.Should().Be(ListingStatus.Active);
        target.Reservations.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMemberHasActiveReservations_When_Reserving_Then_TheyDoNotCountAgainstBuyer()
    {
        var buyer = BuildBuyer();
        var otherBuyer = Member.Create("Other", "other@uni.edu", Now).Value;
        var otherListing = BuildActiveListing(OtherSellerId);
        otherListing.Reserve(otherBuyer.Id, Now);
        var target = BuildActiveListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[otherListing.Id] = otherListing;
        listings.Items[target.Id] = target;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(target.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BuyerId.Should().Be(buyer.Id);
        target.Status.Should().Be(ListingStatus.Reserved);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_BuyerWithTwoActiveAndOneCancelled_When_Reserving_Then_ReservesListingAndSavesOnce()
    {
        var buyer = BuildBuyer();
        var listingA = BuildActiveListing(OtherSellerId);
        listingA.Reserve(buyer.Id, Now);
        var listingB = BuildActiveListing(OtherSellerId);
        listingB.Reserve(buyer.Id, Now);
        var listingC = BuildActiveListing(OtherSellerId);
        var reservationC = listingC.Reserve(buyer.Id, Now).Value;
        listingC.CancelReservation(reservationC.Id);
        var target = BuildActiveListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listingA.Id] = listingA;
        listings.Items[listingB.Id] = listingB;
        listings.Items[listingC.Id] = listingC;
        listings.Items[target.Id] = target;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, new FakeNotificationRepository(), unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(target.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BuyerId.Should().Be(buyer.Id);
        result.Value.ListingId.Should().Be(target.Id);
        target.Status.Should().Be(ListingStatus.Reserved);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ActiveListingAndExistingBuyer_When_Reserving_Then_NotifiesSellerOnce()
    {
        var listing = BuildActiveListing(SellerId);
        var buyer = BuildBuyer();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var notifications = new FakeNotificationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, notifications, unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(listing.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notifications.Items.Should().ContainSingle();
        notifications.Items[0].MemberId.Should().Be(SellerId);
        notifications.Items[0].Type.Should().Be(NotificationType.ReservationCreated);
        notifications.Items[0].Message.Should().Be("Buyer reserved Algebra book");
        notifications.Items[0].ListingId.Should().Be(listing.Id);
        notifications.Items[0].CreatedAt.Should().Be(Now);
        notifications.Items[0].ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_DraftListing_When_Reserving_Then_DoesNotNotify()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        var buyer = BuildBuyer();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var notifications = new FakeNotificationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, notifications, unitOfWork, new FixedTimeProvider(Now));
        var command = new ReserveListingCommand(listing.Id, buyer.Id);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotActive");
        result.Error.Message.Should().Be("Only an active listing can be reserved.");
        notifications.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownBuyer_When_Reserving_Then_DoesNotNotify()
    {
        var listing = BuildActiveListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        var notifications = new FakeNotificationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReserveListingHandler(listings, members, notifications, unitOfWork, new FixedTimeProvider(Now));
        var unknownBuyerId = Guid.NewGuid();
        var command = new ReserveListingCommand(listing.Id, unknownBuyerId);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownBuyerId}' was not found.");
        notifications.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
