using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Offers;

// A price proposed for a listing, between its seller and one buyer, inside their conversation.
// The buyer makes the first offer; after that the two sides take turns: a counter-offer marks the
// previous offer Countered and creates a new Pending one from the other side. Only the side that
// did NOT propose an offer can accept, reject or counter it. A buyer has at most one Pending offer
// per listing (checked by the handler).
public sealed class Offer
{
    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid BuyerId { get; private set; }
    public Guid SellerId { get; private set; }
    // Who proposed this amount: the buyer (first offer, counter to a counter) or the seller (counter-offer).
    public Guid ProposedById { get; private set; }
    public decimal Amount { get; private set; }
    public OfferStatus Status { get; private set; }
    // The offer this one answers; null for the buyer's first offer.
    public Guid? ParentOfferId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    // When the offer stopped being Pending (accepted, rejected, countered or withdrawn).
    public DateTimeOffset? RespondedAt { get; private set; }

    private Offer() { }

    // The member who has to answer: the side that did not propose it.
    public Guid RecipientId => ProposedById == BuyerId ? SellerId : BuyerId;

    public bool IsParticipant(Guid memberId) => memberId == BuyerId || memberId == SellerId;

    // The buyer's first offer on a listing.
    public static Result<Offer> Make(Listing listing, Guid buyerId, decimal amount, DateTimeOffset now)
    {
        if (buyerId == Guid.Empty)
            return Result<Offer>.Failure(
                Error.Validation("Offer.BuyerEmpty", "Buyer id must not be empty."));

        if (listing.SellerId == buyerId)
            return Result<Offer>.Failure(
                Error.Validation("Offer.OwnListing", "A seller cannot make an offer on their own listing."));

        var checkedAmount = CheckListingAndAmount(listing, amount);
        if (checkedAmount is not null)
            return Result<Offer>.Failure(checkedAmount);

        return New(listing.Id, buyerId, listing.SellerId, buyerId, amount, null, now);
    }

    // The recipient accepts: the listing is reserved for the buyer at this amount, through the
    // listing's own reservation rules (it must still be Active).
    public Result<Reservation> Accept(Guid memberId, Listing listing, DateTimeOffset now)
    {
        var answer = CheckCanAnswer(memberId);
        if (answer is not null)
            return Result<Reservation>.Failure(answer);

        if (listing.Id != ListingId || listing.Status != ListingStatus.Active)
            return Result<Reservation>.Failure(ListingNotActive());

        // Cannot fail here: the listing is Active (checked above) and the buyer is neither empty nor the
        // seller (checked when the offer was made).
        var reservation = listing.Reserve(BuyerId, now, Money.Create(Amount).Value).Value;

        Status = OfferStatus.Accepted;
        RespondedAt = now;
        return reservation;
    }

    public Result<Offer> Reject(Guid memberId, DateTimeOffset now)
    {
        var answer = CheckCanAnswer(memberId);
        if (answer is not null)
            return Result<Offer>.Failure(answer);

        Status = OfferStatus.Rejected;
        RespondedAt = now;
        return this;
    }

    // The recipient answers with another amount: this offer becomes Countered and the returned
    // Pending offer goes the other way.
    public Result<Offer> Counter(Guid memberId, Listing listing, decimal amount, DateTimeOffset now)
    {
        var answer = CheckCanAnswer(memberId);
        if (answer is not null)
            return Result<Offer>.Failure(answer);

        if (listing.Id != ListingId)
            return Result<Offer>.Failure(ListingNotActive());

        var checkedAmount = CheckListingAndAmount(listing, amount);
        if (checkedAmount is not null)
            return Result<Offer>.Failure(checkedAmount);

        Status = OfferStatus.Countered;
        RespondedAt = now;
        return New(ListingId, BuyerId, SellerId, memberId, amount, Id, now);
    }

    // The buyer takes back their own Pending offer (a counter-offer from the seller is rejected, not withdrawn).
    public Result<Offer> Withdraw(Guid memberId, DateTimeOffset now)
    {
        if (memberId != BuyerId)
            return Result<Offer>.Failure(IsParticipant(memberId)
                ? Error.Forbidden("Offer.NotBuyer", "Only the buyer can withdraw an offer.")
                : NotParticipant());

        if (ProposedById != BuyerId)
            return Result<Offer>.Failure(
                Error.Validation("Offer.NotOwnOffer", "Only your own offer can be withdrawn; reject the seller's counter-offer instead."));

        if (Status != OfferStatus.Pending)
            return Result<Offer>.Failure(NotPending());

        Status = OfferStatus.Withdrawn;
        RespondedAt = now;
        return this;
    }

    // The listing was reserved through another offer: a Pending offer on it can no longer be accepted.
    // Returns false when the offer was not Pending (nothing changed).
    public bool CloseBecauseListingReserved(DateTimeOffset now)
    {
        if (Status != OfferStatus.Pending)
            return false;

        Status = OfferStatus.Rejected;
        RespondedAt = now;
        return true;
    }

    // One of the two members blocked the other: a Pending offer between them can no longer be answered.
    // Returns false when the offer was not Pending (nothing changed).
    public bool CloseBecauseBlocked(DateTimeOffset now)
    {
        if (Status != OfferStatus.Pending)
            return false;

        Status = OfferStatus.Rejected;
        RespondedAt = now;
        return true;
    }

    private static Offer New(Guid listingId, Guid buyerId, Guid sellerId, Guid proposedById, decimal amount, Guid? parentOfferId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = listingId,
        BuyerId = buyerId,
        SellerId = sellerId,
        ProposedById = proposedById,
        Amount = amount,
        Status = OfferStatus.Pending,
        ParentOfferId = parentOfferId,
        CreatedAt = now
    };

    // Who may answer (accept, reject, counter), and only while Pending. Returns null when the member can answer.
    public Error? CheckCanAnswer(Guid memberId)
    {
        if (!IsParticipant(memberId))
            return NotParticipant();
        if (memberId != RecipientId)
            return Error.Forbidden("Offer.NotRecipient", "Only the other side can answer this offer.");
        if (Status != OfferStatus.Pending)
            return NotPending();
        return null;
    }

    // Offers are made only on an Active listing, for an amount greater than 0 and at most Money.MaxAmount.
    private static Error? CheckListingAndAmount(Listing listing, decimal amount)
    {
        if (listing.Status != ListingStatus.Active)
            return ListingNotActive();
        if (amount <= 0)
            return Error.Validation("Offer.AmountInvalid", "The offered amount must be greater than 0.");
        if (amount > Money.MaxAmount)
            return Error.Validation("Offer.AmountTooHigh", $"The offered amount must be at most {Money.MaxAmount} lei.");
        return null;
    }

    private static Error ListingNotActive() =>
        Error.Validation("Offer.ListingNotActive", "Offers can only be made and accepted on an active listing.");

    private static Error NotPending() =>
        Error.Validation("Offer.NotPending", "The offer was already answered.");

    private static Error NotParticipant() =>
        Error.Forbidden("Offer.NotParticipant", "Only the buyer and the seller can act on this offer.");
}
