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
                new Error("Listing.SellerEmpty", "Seller id must not be empty."));

        if (!Enum.IsDefined(category))
            return Result<Listing>.Failure(
                new Error("Listing.CategoryInvalid", "Unknown category."));

        if (!Enum.IsDefined(condition))
            return Result<Listing>.Failure(
                new Error("Listing.ConditionInvalid", "Unknown condition."));

        var titleCheck = ValidateTitle(title);
        if (titleCheck.IsFailure)
            return Result<Listing>.Failure(titleCheck.Error);

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


    public Result UpdateDetails(string title, Category category, Condition condition, decimal price)
    {
        if (Status == ListingStatus.Sold)
            return Result.Failure(
                new Error("Listing.Sold", "A sold listing cannot be updated."));

        if (!Enum.IsDefined(category))
            return Result.Failure(
                new Error("Listing.CategoryInvalid", "Unknown category."));

        if (!Enum.IsDefined(condition))
            return Result.Failure(
                new Error("Listing.ConditionInvalid", "Unknown condition."));

        var titleCheck = ValidateTitle(title);
        if (titleCheck.IsFailure)
            return titleCheck;

        var money = Money.Create(price);
        if (money.IsFailure)
            return Result.Failure(money.Error);

        Title = title.Trim();
        Category = category;
        Condition = condition;
        Price = money.Value!;
        Version++;

        return Result.Success();
    }

    public Result Publish()
    {
        if (Status != ListingStatus.Draft)
            return Result.Failure(
                new Error("Listing.NotDraft", "Only a draft listing can be published."));

        Status = ListingStatus.Active;
        Version++;

        return Result.Success();
    }

    private static Result ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure(
                new Error("Listing.TitleEmpty", "Title must not be empty."));

        if (title.Trim().Length > MaxTitleLength)
            return Result.Failure(
                new Error("Listing.TitleTooLong",
                    $"Title must have at most {MaxTitleLength} characters."));

        return Result.Success();
    }
}