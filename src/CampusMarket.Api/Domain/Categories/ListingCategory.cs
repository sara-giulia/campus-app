using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Categories;

/// <summary>
/// The admin-managed settings of one of the 5 base categories (the <see cref="Category"/> enum).
/// The base categories are fixed, because the ML models are trained on them: they are never added
/// or deleted, an Admin only changes the display name, the description and whether new listings
/// can be put in the category.
/// </summary>
public sealed class ListingCategory
{
    public const int MaxDisplayNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public Category Key { get; private set; }
    // Stryker disable once String : the factory always sets DisplayName, so the default value is never observable
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    private ListingCategory() { }

    public static Result<ListingCategory> Create(Category key, string? displayName, string? description = null, bool isActive = true)
    {
        if (!Enum.IsDefined(key))
            return Result<ListingCategory>.Failure(NotFound(key.ToString()));

        var textError = ValidateText(displayName, description);
        if (textError is not null)
            return Result<ListingCategory>.Failure(textError);

        return new ListingCategory
        {
            Key = key,
            DisplayName = displayName!.Trim(),
            Description = NormalizeDescription(description),
            IsActive = isActive
        };
    }

    /// <summary>
    /// Changes the display name, the description and the active flag. <paramref name="allCategories"/>
    /// are all the categories (this one may be among them): at least one category must stay active,
    /// so deactivating the last active one fails with Category.LastActive.
    /// </summary>
    public Result<ListingCategory> Update(
        string? displayName, string? description, bool isActive, IEnumerable<ListingCategory> allCategories)
    {
        var textError = ValidateText(displayName, description);
        if (textError is not null)
            return Result<ListingCategory>.Failure(textError);

        if (IsActive && !isActive && !allCategories.Any(c => c.Key != Key && c.IsActive))
            return Result<ListingCategory>.Failure(
                Error.Conflict("Category.LastActive", "At least one category must stay active."));

        DisplayName = displayName!.Trim();
        Description = NormalizeDescription(description);
        IsActive = isActive;

        return this;
    }

    /// <summary>
    /// Returns null when a listing can be created in this category or moved into it,
    /// otherwise the Listing.CategoryInactive error. Listings that are already in the
    /// category are not affected.
    /// </summary>
    public Error? CheckAcceptsListings() =>
        IsActive
            ? null
            : Error.Validation("Listing.CategoryInactive", $"The category '{Key}' is not active, a listing cannot be put in it.");

    public static Error NotFound(string key) =>
        Error.NotFound("Category.NotFound", $"Category '{key}' was not found.");

    private static Error? ValidateText(string? displayName, string? description)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return Error.Validation("Category.DisplayNameEmpty", "Display name must not be empty.");
        if (displayName.Trim().Length > MaxDisplayNameLength)
            return Error.Validation("Category.DisplayNameTooLong", $"Display name must have at most {MaxDisplayNameLength} characters.");
        if (description is not null && description.Trim().Length > MaxDescriptionLength)
            return Error.Validation("Category.DescriptionTooLong", $"Description must have at most {MaxDescriptionLength} characters.");

        return null;
    }

    // An empty or blank description is stored as null, so "no description" has only one form.
    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
