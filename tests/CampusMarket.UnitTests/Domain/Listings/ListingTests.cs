using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Listings;

public class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Listing DraftListing()
    {
        return Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    private static Listing ActiveListing()
    {
        var listing = DraftListing();
        listing.Publish();
        return listing;
    }

    [Fact]
    public void Given_ValidData_When_Creating_Then_ReturnsDraftListingWithVersionOne()
    {
        var sellerId = Guid.NewGuid();

        var result = Listing.Create(sellerId, "  Algebra book  ", Category.Textbook, Condition.Good, 25m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.SellerId.Should().Be(sellerId);
        result.Value.Title.Should().Be("Algebra book");
        result.Value.Category.Should().Be(Category.Textbook);
        result.Value.Condition.Should().Be(Condition.Good);
        result.Value.Price.Amount.Should().Be(25m);
        result.Value.Status.Should().Be(ListingStatus.Draft);
        result.Value.Version.Should().Be(1);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void Given_EmptySellerId_When_Creating_Then_ReturnsValidationError()
    {
        var result = Listing.Create(Guid.Empty, "Algebra book", Category.Textbook, Condition.Good, 25m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.SellerEmpty");
        result.Error.Message.Should().Be("Seller id must not be empty.");
    }

    [Fact]
    public void Given_UndefinedCategory_When_Creating_Then_ReturnsValidationError()
    {
        var result = Listing.Create(Guid.NewGuid(), "Algebra book", (Category)99, Condition.Good, 25m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInvalid");
        result.Error.Message.Should().Be("Unknown category.");
    }

    [Fact]
    public void Given_UndefinedCondition_When_Creating_Then_ReturnsValidationError()
    {
        var result = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, (Condition)99, 25m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.ConditionInvalid");
        result.Error.Message.Should().Be("Unknown condition.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyTitle_When_Creating_Then_ReturnsValidationError(string? title)
    {
        var result = Listing.Create(Guid.NewGuid(), title!, Category.Textbook, Condition.Good, 25m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleEmpty");
        result.Error.Message.Should().Be("Title must not be empty.");
    }

    [Fact]
    public void Given_TitleLongerThanMax_When_Creating_Then_ReturnsValidationError()
    {
        var longTitle = new string('a', Listing.MaxTitleLength + 1);

        var result = Listing.Create(Guid.NewGuid(), longTitle, Category.Textbook, Condition.Good, 25m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.TitleTooLong");
        result.Error.Message.Should().Be("Title must have at most 100 characters.");
    }

    [Fact]
    public void Given_TitleAtMaxLength_When_Creating_Then_Succeeds()
    {
        var title = new string('a', Listing.MaxTitleLength);

        var result = Listing.Create(Guid.NewGuid(), title, Category.Textbook, Condition.Good, 25m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be(title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_InvalidPrice_When_Creating_Then_ReturnsValidationError(decimal price)
    {
        var result = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, price, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Money.Invalid");
        result.Error.Message.Should().Be("Price must be greater than 0.");
    }

    [Fact]
    public void Given_NoDescription_When_Creating_Then_DescriptionIsNull()
    {
        var result = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public void Given_DescriptionWithWhitespace_When_Creating_Then_StoredTrimmed()
    {
        var result = Listing.Create(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now,
            "  Looks almost new, no highlights.  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Looks almost new, no highlights.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_BlankDescription_When_Creating_Then_StoredAsNull(string description)
    {
        var result = Listing.Create(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now, description);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public void Given_DescriptionAtMaxLength_When_Creating_Then_Succeeds()
    {
        var description = new string('a', Listing.MaxDescriptionLength);

        var result = Listing.Create(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now, description);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(description);
    }

    [Fact]
    public void Given_DescriptionLongerThanMax_When_Creating_Then_ReturnsValidationError()
    {
        var description = new string('a', Listing.MaxDescriptionLength + 1);

        var result = Listing.Create(
            Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now, description);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be("Description must have at most 1000 characters.");
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingDetailsWithValidData_Then_UpdatesFieldsAndIncrementsVersion()
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails("  New title  ", Category.Electronics, Condition.New, 50m);

        result.IsSuccess.Should().BeTrue();
        listing.Title.Should().Be("New title");
        listing.Category.Should().Be(Category.Electronics);
        listing.Condition.Should().Be(Condition.New);
        listing.Price.Amount.Should().Be(50m);
        listing.Version.Should().Be(2);
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithUndefinedCategory_Then_ReturnsValidationError()
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails("Algebra book", (Category)99, Condition.Good, 25m);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.CategoryInvalid");
        result.Error.Message.Should().Be("Unknown category.");
        listing.Version.Should().Be(1);
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithUndefinedCondition_Then_ReturnsValidationError()
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails("Algebra book", Category.Textbook, (Condition)99, 25m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.ConditionInvalid");
        result.Error.Message.Should().Be("Unknown condition.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Given_DraftListing_When_UpdatingWithEmptyTitle_Then_ReturnsValidationError(string? title)
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails(title!, Category.Textbook, Condition.Good, 25m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.TitleEmpty");
        result.Error.Message.Should().Be("Title must not be empty.");
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithTitleTooLong_Then_ReturnsValidationError()
    {
        var listing = DraftListing();
        var longTitle = new string('a', Listing.MaxTitleLength + 1);

        var result = listing.UpdateDetails(longTitle, Category.Textbook, Condition.Good, 25m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Listing.TitleTooLong");
        result.Error.Message.Should().Be("Title must have at most 100 characters.");
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithInvalidPrice_Then_ReturnsValidationError()
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails("Algebra book", Category.Textbook, Condition.Good, 0m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Money.Invalid");
        result.Error.Message.Should().Be("Price must be greater than 0.");
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithDescription_Then_StoresTrimmedDescription()
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails(
            "Algebra book", Category.Textbook, Condition.Good, 25m, "  Great condition.  ");

        result.IsSuccess.Should().BeTrue();
        listing.Description.Should().Be("Great condition.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_DraftListing_When_UpdatingWithBlankDescription_Then_DescriptionIsNull(string description)
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails(
            "Algebra book", Category.Textbook, Condition.Good, 25m, description);

        result.IsSuccess.Should().BeTrue();
        listing.Description.Should().BeNull();
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithDescriptionAtMaxLength_Then_Succeeds()
    {
        var listing = DraftListing();
        var description = new string('a', Listing.MaxDescriptionLength);

        var result = listing.UpdateDetails(
            "Algebra book", Category.Textbook, Condition.Good, 25m, description);

        result.IsSuccess.Should().BeTrue();
        listing.Description.Should().Be(description);
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithDescriptionTooLong_Then_ReturnsValidationError()
    {
        var listing = DraftListing();
        var description = new string('a', Listing.MaxDescriptionLength + 1);

        var result = listing.UpdateDetails(
            "Algebra book", Category.Textbook, Condition.Good, 25m, description);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.DescriptionTooLong");
        result.Error.Message.Should().Be("Description must have at most 1000 characters.");
        listing.Description.Should().BeNull();
        listing.Version.Should().Be(1);
    }

    [Fact]
    public void Given_DraftListing_When_UpdatingWithoutDescription_Then_DescriptionIsNull()
    {
        var listing = DraftListing();

        var result = listing.UpdateDetails(
            "Algebra book", Category.Textbook, Condition.Good, 25m);

        result.IsSuccess.Should().BeTrue();
        listing.Description.Should().BeNull();
    }

    [Fact]
    public void Given_DraftListing_When_Publishing_Then_BecomesActiveAndIncrementsVersion()
    {
        var listing = DraftListing();

        var result = listing.Publish();

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(2);
    }

    [Fact]
    public void Given_ActiveListing_When_Publishing_Then_ReturnsValidationError()
    {
        var listing = ActiveListing();

        var result = listing.Publish();

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotDraft");
        result.Error.Message.Should().Be("Only a draft listing can be published.");
        listing.Version.Should().Be(2);
    }

    [Fact]
    public void Given_ActiveListing_When_BuyerReserves_Then_ReservationIsCreatedAndListingBecomesReserved()
    {
        var listing = ActiveListing();
        var buyerId = Guid.NewGuid();

        var result = listing.Reserve(buyerId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.MemberId.Should().Be(buyerId);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.Status.Should().Be(ReservationStatus.Active);
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Version.Should().Be(3);
        listing.Reservations.Should().ContainSingle();
    }

    [Fact]
    public void Given_DraftListing_When_Reserving_Then_ReturnsValidationError()
    {
        var listing = DraftListing();

        var result = listing.Reserve(Guid.NewGuid(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotActive");
        result.Error.Message.Should().Be("Only an active listing can be reserved.");
    }

    [Fact]
    public void Given_ActiveListing_When_ReservingWithEmptyBuyerId_Then_ReturnsValidationError()
    {
        var listing = ActiveListing();

        var result = listing.Reserve(Guid.Empty, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reservation.InvalidBuyer");
        result.Error.Message.Should().Be("Buyer id must not be empty.");
    }

    [Fact]
    public void Given_ActiveListing_When_SellerReservesOwnListing_Then_ReturnsValidationError()
    {
        var listing = ActiveListing();

        var result = listing.Reserve(listing.SellerId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reservation.OwnListing");
        result.Error.Message.Should().Be("A seller cannot reserve their own listing.");
    }

    [Fact]
    public void Given_ReservedListing_When_CancellingReservation_Then_BecomesActiveAndIncrementsVersion()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;

        var result = listing.CancelReservation(reservation.Id);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Version.Should().Be(4);
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public void Given_ReservedListing_When_CancellingUnknownReservation_Then_ReturnsNotFound()
    {
        var listing = ActiveListing();
        listing.Reserve(Guid.NewGuid(), Now);

        var result = listing.CancelReservation(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Reservation.NotFound");
        result.Error.Message.Should().Be("Reservation not found.");
    }

    [Fact]
    public void Given_ListingWithCancelledReservation_When_CancellingItAgain_Then_ReturnsValidationError()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.CancelReservation(reservation.Id);

        var result = listing.CancelReservation(reservation.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can be cancelled.");
    }

    [Fact]
    public void Given_ReservedListing_When_ConfirmingSaleBeforeExpiry_Then_BecomesSoldAndReservationCompleted()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        var versionBefore = listing.Version;

        var result = listing.ConfirmSale(reservation.Id, Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Sold);
        listing.Version.Should().Be(versionBefore + 1);
        reservation.Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public void Given_ReservedListing_When_ConfirmingSaleOneSecondBeforeExpiry_Then_Succeeds()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;

        var result = listing.ConfirmSale(reservation.Id, Now.AddHours(48).AddSeconds(-1));

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Sold);
        reservation.Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public void Given_ReservedListing_When_ConfirmingSaleWithUnknownReservationId_Then_ReturnsReservationNotFound()
    {
        var listing = ActiveListing();
        listing.Reserve(Guid.NewGuid(), Now);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = listing.ConfirmSale(Guid.NewGuid(), Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Reservation.NotFound");
        result.Error.Message.Should().Be("Reservation not found.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void Given_ListingWithCancelledReservation_When_ConfirmingSale_Then_ReturnsReservationNotActive()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.CancelReservation(reservation.Id);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = listing.ConfirmSale(reservation.Id, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can be completed.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void Given_SoldListing_When_ConfirmingSaleAgain_Then_ReturnsReservationNotActive()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.ConfirmSale(reservation.Id, Now.AddHours(1));
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = listing.ConfirmSale(reservation.Id, Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can be completed.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void Given_ReservedListing_When_ConfirmingSaleAtExactExpiry_Then_ReturnsReservationExpired()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = listing.ConfirmSale(reservation.Id, Now.AddHours(48));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.Expired");
        result.Error.Message.Should().Be("The reservation has expired.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void Given_SoldListing_When_UpdatingDetailsWithValidData_Then_ReturnsListingSoldAndDoesNotChange()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.ConfirmSale(reservation.Id, Now.AddHours(1));
        var titleBefore = listing.Title;
        var categoryBefore = listing.Category;
        var conditionBefore = listing.Condition;
        var priceBefore = listing.Price.Amount;
        var versionBefore = listing.Version;

        var result = listing.UpdateDetails("New title", Category.Electronics, Condition.New, 50m);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.Sold");
        result.Error.Message.Should().Be("A sold listing cannot be changed.");
        listing.Title.Should().Be(titleBefore);
        listing.Category.Should().Be(categoryBefore);
        listing.Condition.Should().Be(conditionBefore);
        listing.Price.Amount.Should().Be(priceBefore);
        listing.Version.Should().Be(versionBefore);
    }

    [Fact]
    public void Given_SoldListing_When_UpdatingDetailsWithEmptyTitle_Then_ReturnsListingSoldBeforeTitleCheck()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.ConfirmSale(reservation.Id, Now.AddHours(1));

        var result = listing.UpdateDetails("", Category.Textbook, Condition.Good, 25m);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.Sold");
        result.Error.Message.Should().Be("A sold listing cannot be changed.");
    }

    [Fact]
    public void Given_SoldListing_When_Publishing_Then_ReturnsNotDraftError()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.ConfirmSale(reservation.Id, Now.AddHours(1));

        var result = listing.Publish();

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotDraft");
        result.Error.Message.Should().Be("Only a draft listing can be published.");
    }

    [Fact]
    public void Given_SoldListing_When_Reserving_Then_ReturnsNotActiveError()
    {
        var listing = ActiveListing();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;
        listing.ConfirmSale(reservation.Id, Now.AddHours(1));

        var result = listing.Reserve(Guid.NewGuid(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listing.NotActive");
        result.Error.Message.Should().Be("Only an active listing can be reserved.");
    }
}
