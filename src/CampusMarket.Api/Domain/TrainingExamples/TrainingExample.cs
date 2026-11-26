using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.TrainingExamples;

// One labelled example for retraining a model later (feedback loop).
// Spam: the Admin's decision on the reports of a listing ("spam" when confirmed, "not spam" when dismissed).
// Category: the category the seller picked when it differs from the suggested one.
// No personal data: only the listing text and ids (CreatedBy is the member id, never a name or an email).
// At most one example per listing and kind: a later decision relabels the same example.
public sealed class TrainingExample
{
    public const string SpamLabel = "spam";
    public const string NotSpamLabel = "not spam";
    public const int MaxLabelLength = 32;

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public TrainingExampleKind Kind { get; private set; }
    // Stryker disable once String : the factories always set Title, so the default value is never observable
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    // Stryker disable once String : the factories always set Label, so the default value is never observable
    public string Label { get; private set; } = string.Empty;
    public TrainingExampleSource Source { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    // Set when a later decision relabels the example; null while it has its first label.
    public DateTimeOffset? UpdatedAt { get; private set; }

    private TrainingExample() { }

    public static Result<TrainingExample> ForSpamDecision(
        Listing listing, bool isSpam, Guid adminId, DateTimeOffset createdAt)
    {
        var error = Validate(listing.Id, listing.Title, listing.Description, adminId);
        if (error is not null)
            return Result<TrainingExample>.Failure(error);

        return New(listing, TrainingExampleKind.Spam, SpamLabelFor(isSpam), TrainingExampleSource.Report, adminId, createdAt);
    }

    // suggestedCategory is the raw value the client sent (it may be missing or not a category).
    // Fails when there is nothing to learn: no valid suggestion, or the seller kept the suggested category.
    public static Result<TrainingExample> ForCategoryChoice(
        Listing listing, string? suggestedCategory, Guid sellerId, DateTimeOffset createdAt)
    {
        var error = CheckCategoryChoice(listing, suggestedCategory)
                    ?? Validate(listing.Id, listing.Title, listing.Description, sellerId);
        if (error is not null)
            return Result<TrainingExample>.Failure(error);

        return New(listing, TrainingExampleKind.Category, listing.Category.ToString(),
            TrainingExampleSource.SellerChoice, sellerId, createdAt);
    }

    // A later Admin decision on the same listing replaces the earlier label and text.
    public Result<TrainingExample> RelabelSpam(Listing listing, bool isSpam, Guid adminId, DateTimeOffset updatedAt)
    {
        if (Kind != TrainingExampleKind.Spam || listing.Id != ListingId)
            return Result<TrainingExample>.Failure(
                Error.Validation("TrainingExample.KindMismatch", "This example is not the spam example of this listing."));

        var error = Validate(listing.Id, listing.Title, listing.Description, adminId);
        if (error is not null)
            return Result<TrainingExample>.Failure(error);

        Apply(listing, SpamLabelFor(isSpam), adminId, updatedAt);
        return this;
    }

    // A later seller choice on the same listing replaces the earlier label and text.
    public Result<TrainingExample> RelabelCategory(
        Listing listing, string? suggestedCategory, Guid sellerId, DateTimeOffset updatedAt)
    {
        if (Kind != TrainingExampleKind.Category || listing.Id != ListingId)
            return Result<TrainingExample>.Failure(
                Error.Validation("TrainingExample.KindMismatch", "This example is not the category example of this listing."));

        var error = CheckCategoryChoice(listing, suggestedCategory)
                    ?? Validate(listing.Id, listing.Title, listing.Description, sellerId);
        if (error is not null)
            return Result<TrainingExample>.Failure(error);

        Apply(listing, listing.Category.ToString(), sellerId, updatedAt);
        return this;
    }

    // A suggestion counts only when it names a known category (by name, case-insensitive).
    public static Category? ParseSuggestedCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Only an exact name: Enum.TryParse would also accept numbers ("3") and lists ("Textbook,Notes").
        var trimmed = value.Trim();
        foreach (var category in Enum.GetValues<Category>())
        {
            if (string.Equals(category.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                return category;
        }

        return null;
    }

    private static Error? CheckCategoryChoice(Listing listing, string? suggestedCategory)
    {
        var suggested = ParseSuggestedCategory(suggestedCategory);
        if (suggested is null)
            return Error.Validation("TrainingExample.SuggestionInvalid", "No valid suggested category was sent.");
        if (suggested.Value == listing.Category)
            return Error.Validation("TrainingExample.SameAsSuggested", "The chosen category is the suggested one.");
        return null;
    }

    private static Error? Validate(Guid listingId, string title, string? description, Guid createdBy)
    {
        if (listingId == Guid.Empty)
            return Error.Validation("TrainingExample.ListingEmpty", "Listing id must not be empty.");
        if (createdBy == Guid.Empty)
            return Error.Validation("TrainingExample.CreatedByEmpty", "Created by must not be empty.");
        return Listing.ValidateText(title, description);
    }

    private static string SpamLabelFor(bool isSpam) => isSpam ? SpamLabel : NotSpamLabel;

    private static TrainingExample New(
        Listing listing, TrainingExampleKind kind, string label, TrainingExampleSource source,
        Guid createdBy, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = listing.Id,
        Kind = kind,
        Title = listing.Title,
        Description = listing.Description,
        Label = label,
        Source = source,
        CreatedBy = createdBy,
        CreatedAt = createdAt
    };

    private void Apply(Listing listing, string label, Guid by, DateTimeOffset at)
    {
        Title = listing.Title;
        Description = listing.Description;
        Label = label;
        CreatedBy = by;
        UpdatedAt = at;
    }
}
