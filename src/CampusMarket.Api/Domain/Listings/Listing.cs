using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Listings;

public sealed class Listing
{
    public const int MaxTitleLength = 100;
    public const int MaxDescriptionLength = 1000;

    private readonly List<Reservation> _reservations = new();
    public IReadOnlyList<Reservation> Reservations => _reservations.AsReadOnly();

    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    // Stryker disable once String : the factory always sets Title, so the default value is never observable
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Category Category { get; private set; }
    public Condition Condition { get; private set; }
    public Money Price { get; private set; } = null!;
    public ListingStatus Status { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    // Set when the listing is published or renewed; null for a draft that was never published.
    public DateTimeOffset? ExpiresAt { get; private set; }

    private Listing() { }

    public static Result<Listing> Create(
        Guid sellerId,
        string title,
        Category category,
        Condition condition,
        decimal price,
        DateTimeOffset createdAt,
        string? description = null)
    {
        if (sellerId == Guid.Empty)
            return Result<Listing>.Failure(
                Error.Validation("Listing.SellerEmpty", "Seller id must not be empty."));

        var details = ValidateDetails(title, description, category, condition, price);
        if (details.IsFailure)
            return Result<Listing>.Failure(details.Error);

        return new Listing
        {
            Id = Guid.NewGuid(),
            SellerId = sellerId,
            Title = title.Trim(),
            Description = NormalizeDescription(description),
            Category = category,
            Condition = condition,
            Price = details.Value,
            Status = ListingStatus.Draft,
            Version = 1,
            CreatedAt = createdAt
        };
    }

    public Result<Listing> UpdateDetails(string title, Category category, Condition condition, decimal price, string? description = null)    {
        if (Status == ListingStatus.Sold)
            return Result<Listing>.Failure(
                Error.Validation("Listing.Sold", "A sold listing cannot be changed."));

        var details = ValidateDetails(title, description, category, condition, price);
        if (details.IsFailure)
            return Result<Listing>.Failure(details.Error);

        Title = title.Trim();
        Description = NormalizeDescription(description);
        Category = category;
        Condition = condition;
        Price = details.Value;
        Version++;

        return this;
    }

    // expiresAt is computed by the caller (now + Listings:ExpiryDays).
    public Result<Listing> Publish(DateTimeOffset expiresAt)
    {
        if (Status != ListingStatus.Draft)
            return Result<Listing>.Failure(
                Error.Validation("Listing.NotDraft", "Only a draft listing can be published."));

        Status = ListingStatus.Active;
        ExpiresAt = expiresAt;
        Version++;

        return this;
    }

    // Only an Active listing expires (Reserved, Sold, Draft and Hidden do not). Called by the background
    // job (Features/Listings/ExpireListings). Returns false when nothing changed.
    public bool Expire(DateTimeOffset currentTime)
    {
        // A lifted comparison: with no ExpiresAt (null) it is false too, so such a listing never expires.
        if (Status != ListingStatus.Active || !(ExpiresAt <= currentTime))
            return false;

        Status = ListingStatus.Expired;
        Version++;

        return true;
    }

    // The seller puts an expired listing back in the search for another period.
    public Result<Listing> Renew(DateTimeOffset expiresAt)
    {
        if (Status != ListingStatus.Expired)
            return Result<Listing>.Failure(
                Error.Validation("Listing.NotExpired", "Only an expired listing can be renewed."));

        Status = ListingStatus.Active;
        ExpiresAt = expiresAt;
        Version++;

        return this;
    }

    public Result<Reservation> Reserve(Guid buyerId, DateTimeOffset currentTime)
    {
        if (Status != ListingStatus.Active)
            return Result<Reservation>.Failure(
                Error.Validation("Listing.NotActive", "Only an active listing can be reserved."));
        if (buyerId == Guid.Empty)
            return Result<Reservation>.Failure(
                Error.Validation("Reservation.InvalidBuyer", "Buyer id must not be empty."));
        if (buyerId == SellerId)
            return Result<Reservation>.Failure(
                Error.Validation("Reservation.OwnListing", "A seller cannot reserve their own listing."));

        var reservation = Reservation.Create(Id, buyerId, currentTime);
        _reservations.Add(reservation);
        Status = ListingStatus.Reserved;
        Version++;

        return reservation;
    }

    public Result<Listing> ConfirmSale(Guid reservationId, DateTimeOffset currentTime)
    {
        var reservation = _reservations.FirstOrDefault(r => r.Id == reservationId);
        if (reservation is null)
            return Result<Listing>.Failure(
                Error.NotFound("Reservation.NotFound", "Reservation not found."));
        
        if (reservation.IsExpired(currentTime))
            return Result<Listing>.Failure(
                Error.Validation("Reservation.Expired", "The reservation has expired."));

        // A listing has at most one active reservation (Reserve needs an Active listing and sets it to Reserved),
        // so there are no other active reservations to cancel here.
        var completed = reservation.Complete();
        if (completed.IsFailure)
            return Result<Listing>.Failure(completed.Error);

        Status = ListingStatus.Sold;
        Version++;

        return this;
    }

    // A listing has at most one active reservation (Reserve needs an Active listing and sets it to Reserved).
    // Returns the number of reservations that were expired (0 or 1).
    public int ExpireReservations(DateTimeOffset currentTime)
    {
        var active = _reservations.FirstOrDefault(r => r.Status == ReservationStatus.Active);
        if (active is null || !active.IsExpired(currentTime))
            return 0;

        active.Expire();
        Status = ListingStatus.Active;
        Version++;

        return 1;
    }

    public Result<Listing> CancelReservation(Guid reservationId)
    {
        var reservation = _reservations.FirstOrDefault(r => r.Id == reservationId);
        if (reservation is null)
            return Result<Listing>.Failure(
                Error.NotFound("Reservation.NotFound", "Reservation not found."));

        // Reservation.Cancel already refuses a reservation that is not active.
        // An active reservation always means the listing is Reserved, so no extra check is needed.
        var cancelResult = reservation.Cancel();
        if (cancelResult.IsFailure)
            return Result<Listing>.Failure(cancelResult.Error);

        Status = ListingStatus.Active;
        Version++;

        return this;
    }

    // Only an Admin hides a listing (Features/Moderation/HideListing). A hidden listing is not in the search,
    // cannot be reserved, reported or added to favorites. A reserved listing loses its active reservation.
    public Result<Listing> Hide()
    {
        if (Status == ListingStatus.Hidden)
            return Result<Listing>.Failure(
                Error.Validation("Listing.AlreadyHidden", "The listing is already hidden."));
        if (Status is not (ListingStatus.Active or ListingStatus.Reserved))
            return Result<Listing>.Failure(
                Error.Validation("Listing.CannotHide", "Only an active or reserved listing can be hidden."));

        // A Reserved listing has exactly one active reservation (see Reserve).
        _reservations.FirstOrDefault(r => r.Status == ReservationStatus.Active)?.Cancel();

        Status = ListingStatus.Hidden;
        Version++;

        return this;
    }

    // Checks the details shared by Create and UpdateDetails.
    // On success it returns the validated price, so the caller does not create Money twice.
    private static Result<Money> ValidateDetails(string? title, string? description, Category category, Condition condition, decimal price)    {
        if (!Enum.IsDefined(category))
            return Result<Money>.Failure(
                Error.Validation("Listing.CategoryInvalid", "Unknown category."));
        if (!Enum.IsDefined(condition))
            return Result<Money>.Failure(
                Error.Validation("Listing.ConditionInvalid", "Unknown condition."));

        var textError = ValidateText(title, description);
        if (textError is not null)
            return Result<Money>.Failure(textError);

        return Money.Create(price);
    }

    // Category and condition must be sent (the request fields are nullable so that a missing one is an
    // error, not the first enum value). Used by create, update and the price suggestion.
    public static Result<(Category Category, Condition Condition)> RequireClassification(Category? category, Condition? condition)
    {
        if (category is null)
            return Result<(Category, Condition)>.Failure(
                Error.Validation("Listing.CategoryRequired", "Category is required."));
        if (condition is null)
            return Result<(Category, Condition)>.Failure(
                Error.Validation("Listing.ConditionRequired", "Condition is required."));

        return (category.Value, condition.Value);
    }

    // A draft is private: only its seller or an Admin sees it. Everyone else gets "not found".
    public bool IsVisibleTo(Guid? requesterId, bool isAdmin) =>
        Status != ListingStatus.Draft || isAdmin || requesterId == SellerId;

    // The title and description rules, in one place. The suggestion endpoints (price, category)
    // use them too, so a caller gets the same errors there as when creating a listing.
    // Returns null when both are valid.
    public static Error? ValidateText(string? title, string? description)
    {
        if (description is not null && description.Trim().Length > MaxDescriptionLength)
            return Error.Validation("Listing.DescriptionTooLong", $"Description must have at most {MaxDescriptionLength} characters.");

        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Listing.TitleEmpty", "Title must not be empty.");
        if (title.Trim().Length > MaxTitleLength)
            return Error.Validation("Listing.TitleTooLong", $"Title must have at most {MaxTitleLength} characters.");

        return null;
    }
    
    // An empty or blank description is stored as null, so "no description" has only one form.
    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}

