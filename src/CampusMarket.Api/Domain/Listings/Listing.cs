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
        // Nothing can make a listing Sold yet. The ConfirmSale slice will add
        // the "Listing.Sold" check here, together with its test.

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

    public Result<Listing> Publish()
    {
        if (Status != ListingStatus.Draft)
            return Result<Listing>.Failure(
                Error.Validation("Listing.NotDraft", "Only a draft listing can be published."));

        Status = ListingStatus.Active;
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

    // Checks the details shared by Create and UpdateDetails.
    // On success it returns the validated price, so the caller does not create Money twice.
    private static Result<Money> ValidateDetails(string? title, string? description, Category category, Condition condition, decimal price)    {
        if (!Enum.IsDefined(category))
            return Result<Money>.Failure(
                Error.Validation("Listing.CategoryInvalid", "Unknown category."));
        if (!Enum.IsDefined(condition))
            return Result<Money>.Failure(
                Error.Validation("Listing.ConditionInvalid", "Unknown condition."));
       
        if (description is not null && description.Trim().Length > MaxDescriptionLength)
            return Result<Money>.Failure(
                Error.Validation("Listing.DescriptionTooLong", $"Description must have at most {MaxDescriptionLength} characters."));
        
        if (string.IsNullOrWhiteSpace(title))
            return Result<Money>.Failure(
                Error.Validation("Listing.TitleEmpty", "Title must not be empty."));
        if (title.Trim().Length > MaxTitleLength)
            return Result<Money>.Failure(
                Error.Validation("Listing.TitleTooLong", $"Title must have at most {MaxTitleLength} characters."));

        return Money.Create(price);
    }
    
    // An empty or blank description is stored as null, so "no description" has only one form.
    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}

