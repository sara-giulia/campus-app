using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.UnitTests.Domain.Offers;

public class OfferTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddHours(1);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = new("44444444-4444-4444-4444-444444444444");

    private static Listing DraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;

    private static Listing ActiveListing()
    {
        var listing = DraftListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    // Moves an Active listing to the given status through the domain.
    private static void MoveTo(Listing listing, ListingStatus status)
    {
        switch (status)
        {
            case ListingStatus.Reserved:
                listing.Reserve(StrangerId, Now);
                break;
            case ListingStatus.Sold:
                listing.ConfirmSale(listing.Reserve(StrangerId, Now).Value.Id, Now);
                break;
            case ListingStatus.Hidden:
                listing.Hide();
                break;
            case ListingStatus.Expired:
                listing.Expire(Now.AddDays(31));
                break;
        }
    }

    private static Listing ListingWithStatus(ListingStatus status)
    {
        if (status == ListingStatus.Draft)
            return DraftListing();

        var listing = ActiveListing();
        MoveTo(listing, status);
        return listing;
    }

    private static Offer PendingOffer(Listing listing, decimal amount = 90m) =>
        Offer.Make(listing, BuyerId, amount, Now).Value;

    // An offer of the buyer brought to the given status through the domain.
    private static Offer OfferWithStatus(Listing listing, OfferStatus status)
    {
        var offer = PendingOffer(listing);
        switch (status)
        {
            case OfferStatus.Accepted:
                offer.Accept(SellerId, listing, Now);
                break;
            case OfferStatus.Rejected:
                offer.Reject(SellerId, Now);
                break;
            case OfferStatus.Countered:
                offer.Counter(SellerId, listing, 95m, Now);
                break;
            case OfferStatus.Withdrawn:
                offer.Withdraw(BuyerId, Now);
                break;
        }

        return offer;
    }

    private static void AssertError(Error error, ErrorType type, string code, string message)
    {
        error.Type.Should().Be(type);
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }

    // ---- Make ----

    [Fact]
    public void Given_ActiveListing_When_MakingOffer_Then_CreatesPendingOfferFromBuyer()
    {
        var listing = ActiveListing();

        var result = Offer.Make(listing, BuyerId, 90m, Now);

        result.IsSuccess.Should().BeTrue();
        var offer = result.Value;
        offer.Id.Should().NotBe(Guid.Empty);
        offer.ListingId.Should().Be(listing.Id);
        offer.BuyerId.Should().Be(BuyerId);
        offer.SellerId.Should().Be(SellerId);
        offer.ProposedById.Should().Be(BuyerId);
        offer.RecipientId.Should().Be(SellerId);
        offer.Amount.Should().Be(90m);
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.ParentOfferId.Should().BeNull();
        offer.CreatedAt.Should().Be(Now);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_EmptyBuyer_When_MakingOffer_Then_ReturnsBuyerEmpty()
    {
        var result = Offer.Make(ActiveListing(), Guid.Empty, 90m, Now);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.BuyerEmpty", "Buyer id must not be empty.");
    }

    [Fact]
    public void Given_SellerAsBuyer_When_MakingOffer_Then_ReturnsOwnListing()
    {
        var result = Offer.Make(ActiveListing(), SellerId, 90m, Now);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.OwnListing", "A seller cannot make an offer on their own listing.");
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Hidden)]
    [InlineData(ListingStatus.Expired)]
    public void Given_ListingNotActive_When_MakingOffer_Then_ReturnsListingNotActive(ListingStatus status)
    {
        var listing = ListingWithStatus(status);
        listing.Status.Should().Be(status);

        var result = Offer.Make(listing, BuyerId, 90m, Now);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.ListingNotActive", "Offers can only be made and accepted on an active listing.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_AmountNotPositive_When_MakingOffer_Then_ReturnsAmountInvalid(int amount)
    {
        var result = Offer.Make(ActiveListing(), BuyerId, amount, Now);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.AmountInvalid", "The offered amount must be greater than 0.");
    }

    [Fact]
    public void Given_AmountAboveMaximum_When_MakingOffer_Then_ReturnsAmountTooHigh()
    {
        var result = Offer.Make(ActiveListing(), BuyerId, Money.MaxAmount + 0.01m, Now);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.AmountTooHigh", $"The offered amount must be at most {Money.MaxAmount} lei.");
    }

    [Fact]
    public void Given_MaximumAmount_When_MakingOffer_Then_Succeeds()
    {
        var result = Offer.Make(ActiveListing(), BuyerId, Money.MaxAmount, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(Money.MaxAmount);
    }

    // ---- Accept ----

    [Fact]
    public void Given_PendingOffer_When_SellerAccepts_Then_ListingReservedAtAgreedPrice()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing, 90m);

        var result = offer.Accept(SellerId, listing, Later);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Accepted);
        offer.RespondedAt.Should().Be(Later);
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Reservations.Should().ContainSingle();
        result.Value.Should().BeSameAs(listing.Reservations[0]);
        result.Value.MemberId.Should().Be(BuyerId);
        result.Value.AgreedPrice.Should().Be(90m);
        result.Value.Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public void Given_SellerCounterOffer_When_BuyerAccepts_Then_ListingReservedForBuyerAtCounterAmount()
    {
        var listing = ActiveListing();
        var counter = PendingOffer(listing, 90m).Counter(SellerId, listing, 95m, Now).Value;

        var result = counter.Accept(BuyerId, listing, Later);

        result.IsSuccess.Should().BeTrue();
        counter.Status.Should().Be(OfferStatus.Accepted);
        counter.RespondedAt.Should().Be(Later);
        result.Value.MemberId.Should().Be(BuyerId);
        result.Value.AgreedPrice.Should().Be(95m);
        listing.Status.Should().Be(ListingStatus.Reserved);
    }

    [Fact]
    public void Given_SellerCounterOffer_When_SellerAccepts_Then_ReturnsNotRecipient()
    {
        var listing = ActiveListing();
        var counter = PendingOffer(listing, 90m).Counter(SellerId, listing, 95m, Now).Value;

        var result = counter.Accept(SellerId, listing, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotRecipient", "Only the other side can answer this offer.");
        counter.Status.Should().Be(OfferStatus.Pending);
        counter.RespondedAt.Should().BeNull();
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public void Given_PendingOffer_When_ProposerAccepts_Then_ReturnsNotRecipientAndChangesNothing()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);

        var result = offer.Accept(BuyerId, listing, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotRecipient", "Only the other side can answer this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        listing.Status.Should().Be(ListingStatus.Active);
        listing.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void Given_PendingOffer_When_StrangerAccepts_Then_ReturnsNotParticipantAndChangesNothing()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);

        var result = offer.Accept(StrangerId, listing, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotParticipant", "Only the buyer and the seller can act on this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        listing.Status.Should().Be(ListingStatus.Active);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Given_OfferNotPending_When_SellerAccepts_Then_ReturnsNotPending(OfferStatus status)
    {
        var listing = ActiveListing();
        var offer = OfferWithStatus(listing, status);
        var respondedAt = offer.RespondedAt;
        var listingStatus = listing.Status;
        var reservations = listing.Reservations.Count;

        var result = offer.Accept(SellerId, listing, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.NotPending", "The offer was already answered.");
        offer.Status.Should().Be(status);
        offer.RespondedAt.Should().Be(respondedAt);
        listing.Status.Should().Be(listingStatus);
        listing.Reservations.Should().HaveCount(reservations);
    }

    [Fact]
    public void Given_ListingReservedMeanwhile_When_SellerAccepts_Then_ReturnsListingNotActiveAndChangesNothing()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);
        listing.Reserve(StrangerId, Now);
        var version = listing.Version;

        var result = offer.Accept(SellerId, listing, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.ListingNotActive", "Offers can only be made and accepted on an active listing.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        listing.Status.Should().Be(ListingStatus.Reserved);
        listing.Version.Should().Be(version);
        listing.Reservations.Should().ContainSingle();
    }

    [Fact]
    public void Given_OtherListing_When_SellerAccepts_Then_ReturnsListingNotActiveAndChangesNothing()
    {
        var offer = PendingOffer(ActiveListing());
        var otherListing = ActiveListing();

        var result = offer.Accept(SellerId, otherListing, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.ListingNotActive", "Offers can only be made and accepted on an active listing.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        otherListing.Status.Should().Be(ListingStatus.Active);
        otherListing.Reservations.Should().BeEmpty();
    }

    // ---- Reject ----

    [Fact]
    public void Given_PendingOffer_When_SellerRejects_Then_OfferRejected()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Reject(SellerId, Later);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(offer);
        offer.Status.Should().Be(OfferStatus.Rejected);
        offer.RespondedAt.Should().Be(Later);
    }

    [Fact]
    public void Given_SellerCounterOffer_When_BuyerRejects_Then_OfferRejected()
    {
        var listing = ActiveListing();
        var counter = PendingOffer(listing).Counter(SellerId, listing, 95m, Now).Value;

        var result = counter.Reject(BuyerId, Later);

        result.IsSuccess.Should().BeTrue();
        counter.Status.Should().Be(OfferStatus.Rejected);
        counter.RespondedAt.Should().Be(Later);
    }

    [Fact]
    public void Given_PendingOffer_When_ProposerRejects_Then_ReturnsNotRecipientAndChangesNothing()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Reject(BuyerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotRecipient", "Only the other side can answer this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_PendingOffer_When_StrangerRejects_Then_ReturnsNotParticipantAndChangesNothing()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Reject(StrangerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotParticipant", "Only the buyer and the seller can act on this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Given_OfferNotPending_When_SellerRejects_Then_ReturnsNotPending(OfferStatus status)
    {
        var offer = OfferWithStatus(ActiveListing(), status);
        var respondedAt = offer.RespondedAt;

        var result = offer.Reject(SellerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.NotPending", "The offer was already answered.");
        offer.Status.Should().Be(status);
        offer.RespondedAt.Should().Be(respondedAt);
    }

    // ---- Counter ----

    [Fact]
    public void Given_PendingOffer_When_SellerCounters_Then_OldCounteredAndNewPendingGoesToBuyer()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing, 90m);

        var result = offer.Counter(SellerId, listing, 95m, Later);

        result.IsSuccess.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Countered);
        offer.RespondedAt.Should().Be(Later);
        var counter = result.Value;
        counter.Id.Should().NotBe(offer.Id);
        counter.ListingId.Should().Be(listing.Id);
        counter.BuyerId.Should().Be(BuyerId);
        counter.SellerId.Should().Be(SellerId);
        counter.ProposedById.Should().Be(SellerId);
        counter.RecipientId.Should().Be(BuyerId);
        counter.Amount.Should().Be(95m);
        counter.Status.Should().Be(OfferStatus.Pending);
        counter.ParentOfferId.Should().Be(offer.Id);
        counter.CreatedAt.Should().Be(Later);
        counter.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CounterChain_When_SidesTakeTurns_Then_ParentsChainAndProposersAlternate()
    {
        var listing = ActiveListing();
        var first = PendingOffer(listing, 90m);
        var second = first.Counter(SellerId, listing, 95m, Now.AddMinutes(1)).Value;

        var third = second.Counter(BuyerId, listing, 92m, Now.AddMinutes(2));

        third.IsSuccess.Should().BeTrue();
        first.ParentOfferId.Should().BeNull();
        second.ParentOfferId.Should().Be(first.Id);
        third.Value.ParentOfferId.Should().Be(second.Id);
        first.ProposedById.Should().Be(BuyerId);
        second.ProposedById.Should().Be(SellerId);
        third.Value.ProposedById.Should().Be(BuyerId);
        third.Value.RecipientId.Should().Be(SellerId);
        first.Status.Should().Be(OfferStatus.Countered);
        second.Status.Should().Be(OfferStatus.Countered);
        third.Value.Status.Should().Be(OfferStatus.Pending);
        third.Value.Amount.Should().Be(92m);
    }

    [Fact]
    public void Given_PendingOffer_When_ProposerCounters_Then_ReturnsNotRecipientAndChangesNothing()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);

        var result = offer.Counter(BuyerId, listing, 80m, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotRecipient", "Only the other side can answer this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_PendingOffer_When_StrangerCounters_Then_ReturnsNotParticipantAndChangesNothing()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);

        var result = offer.Counter(StrangerId, listing, 80m, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotParticipant", "Only the buyer and the seller can act on this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Given_OfferNotPending_When_SellerCounters_Then_ReturnsNotPending(OfferStatus status)
    {
        var listing = ActiveListing();
        var offer = OfferWithStatus(listing, status);
        var respondedAt = offer.RespondedAt;

        var result = offer.Counter(SellerId, listing, 95m, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.NotPending", "The offer was already answered.");
        offer.Status.Should().Be(status);
        offer.RespondedAt.Should().Be(respondedAt);
    }

    [Theory]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Hidden)]
    [InlineData(ListingStatus.Expired)]
    public void Given_ListingNotActiveAnymore_When_SellerCounters_Then_ReturnsListingNotActiveAndChangesNothing(ListingStatus status)
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);
        MoveTo(listing, status);
        listing.Status.Should().Be(status);

        var result = offer.Counter(SellerId, listing, 95m, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.ListingNotActive", "Offers can only be made and accepted on an active listing.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_OtherListing_When_SellerCounters_Then_ReturnsListingNotActiveAndChangesNothing()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Counter(SellerId, ActiveListing(), 95m, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.ListingNotActive", "Offers can only be made and accepted on an active listing.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_AmountNotPositive_When_SellerCounters_Then_ReturnsAmountInvalidAndChangesNothing(int amount)
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);

        var result = offer.Counter(SellerId, listing, amount, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.AmountInvalid", "The offered amount must be greater than 0.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_AmountAboveMaximum_When_SellerCounters_Then_ReturnsAmountTooHighAndChangesNothing()
    {
        var listing = ActiveListing();
        var offer = PendingOffer(listing);

        var result = offer.Counter(SellerId, listing, Money.MaxAmount + 0.01m, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.AmountTooHigh", $"The offered amount must be at most {Money.MaxAmount} lei.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    // ---- Withdraw ----

    [Fact]
    public void Given_PendingOffer_When_BuyerWithdraws_Then_OfferWithdrawn()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Withdraw(BuyerId, Later);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(offer);
        offer.Status.Should().Be(OfferStatus.Withdrawn);
        offer.RespondedAt.Should().Be(Later);
    }

    [Fact]
    public void Given_PendingOffer_When_SellerWithdraws_Then_ReturnsNotBuyerAndChangesNothing()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Withdraw(SellerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotBuyer", "Only the buyer can withdraw an offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_PendingOffer_When_StrangerWithdraws_Then_ReturnsNotParticipantAndChangesNothing()
    {
        var offer = PendingOffer(ActiveListing());

        var result = offer.Withdraw(StrangerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Forbidden, "Offer.NotParticipant", "Only the buyer and the seller can act on this offer.");
        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
    }

    [Fact]
    public void Given_SellerCounterOffer_When_BuyerWithdraws_Then_ReturnsNotOwnOfferAndChangesNothing()
    {
        var listing = ActiveListing();
        var counter = PendingOffer(listing).Counter(SellerId, listing, 95m, Now).Value;

        var result = counter.Withdraw(BuyerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.NotOwnOffer",
            "Only your own offer can be withdrawn; reject the seller's counter-offer instead.");
        counter.Status.Should().Be(OfferStatus.Pending);
        counter.RespondedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Given_OfferNotPending_When_BuyerWithdraws_Then_ReturnsNotPending(OfferStatus status)
    {
        var offer = OfferWithStatus(ActiveListing(), status);
        var respondedAt = offer.RespondedAt;

        var result = offer.Withdraw(BuyerId, Later);

        result.IsFailure.Should().BeTrue();
        AssertError(result.Error, ErrorType.Validation, "Offer.NotPending", "The offer was already answered.");
        offer.Status.Should().Be(status);
        offer.RespondedAt.Should().Be(respondedAt);
    }

    // ---- CloseBecauseListingReserved ----

    [Fact]
    public void Given_PendingOffer_When_ClosingBecauseListingReserved_Then_RejectedAndReturnsTrue()
    {
        var offer = PendingOffer(ActiveListing());

        var closed = offer.CloseBecauseListingReserved(Later);

        closed.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Rejected);
        offer.RespondedAt.Should().Be(Later);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Given_OfferNotPending_When_ClosingBecauseListingReserved_Then_ReturnsFalseAndChangesNothing(OfferStatus status)
    {
        var offer = OfferWithStatus(ActiveListing(), status);
        var respondedAt = offer.RespondedAt;

        var closed = offer.CloseBecauseListingReserved(Later.AddHours(1));

        closed.Should().BeFalse();
        offer.Status.Should().Be(status);
        offer.RespondedAt.Should().Be(respondedAt);
    }
}
