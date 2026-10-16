using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Listings;

public sealed class Listing
{
    public const int MaxTitleLength = 100;

    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
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
        DateTimeOffset createdAt)
    {
        if (sellerId == Guid.Empty)
            return Result<Listing>.Failure(
                Error.Validation("Listing.SellerEmpty", "Seller id must not be empty."));

        if (!Enum.IsDefined(category))
            return Result<Listing>.Failure(
                Error.Validation("Listing.CategoryInvalid", "Unknown category."));
        
        if (!Enum.IsDefined(condition))
            return Result<Listing>.Failure(
                Error.Validation("Listing.ConditionInvalid", "Unknown condition."));

        var titleError = ValidateTitle(title);
        if (titleError is not null)
            return Result<Listing>.Failure(titleError);

        var money = Money.Create(price);
        if (money.IsFailure)
            return Result<Listing>.Failure(money.Error);
        
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            SellerId = sellerId,
            Title = title.Trim(),
            Category = category,
            Condition = condition,
            Price = money.Value!,
            Status = ListingStatus.Draft,
            Version = 1,
            CreatedAt = createdAt
        };

        return Result<Listing>.Success(listing);
    }


    public Result<Listing> UpdateDetails(string title, Category category, Condition condition, decimal price)
    {
        if (Status == ListingStatus.Sold)
            return Result<Listing>.Failure(
                Error.Validation("Listing.Sold", "A sold listing cannot be updated."));

        if (!Enum.IsDefined(category))
            return Result<Listing>.Failure(
                Error.Validation("Listing.CategoryInvalid", "Unknown category."));
        if (!Enum.IsDefined(condition))
            return Result<Listing>.Failure(
                Error.Validation("Listing.ConditionInvalid", "Unknown condition."));

        var titleError = ValidateTitle(title);
        if (titleError is not null)
            return Result<Listing>.Failure(titleError);

        var money = Money.Create(price);
        if (money.IsFailure)
            return Result<Listing>.Failure(money.Error);

        Title = title.Trim();
        Category = category;
        Condition = condition;
        Price = money.Value;
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

    // Returns null when the title is valid, or the error that explains why it is not.
    private static Error? ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Listing.TitleEmpty", "Title must not be empty.");

        if (title.Trim().Length > MaxTitleLength)
            return Error.Validation("Listing.TitleTooLong",
                $"Title must have at most {MaxTitleLength} characters.");

        return null;
    }
}
