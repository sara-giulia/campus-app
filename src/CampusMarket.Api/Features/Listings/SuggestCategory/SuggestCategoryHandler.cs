using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.SuggestCategory;

// Pure query: no state changes, no IUnitOfWork. Validation rules and error codes
// mirror those used by Listing (description too long, title empty / too long),
// in the same order as SuggestPrice, so a caller sees the same errors everywhere.
public sealed class SuggestCategoryHandler(ICategorySuggester suggester)
    : IQueryHandler<SuggestCategoryQuery, SuggestCategoryResponse>
{
    public Task<Result<SuggestCategoryResponse>> HandleAsync(SuggestCategoryQuery query, CancellationToken ct)
    {
        var validation = Validate(query);
        if (validation is not null)
            return Task.FromResult(Result<SuggestCategoryResponse>.Failure(validation));

        var suggestion = suggester.Suggest(query.Title.Trim(), query.Description);
        return Task.FromResult<Result<SuggestCategoryResponse>>(
            new SuggestCategoryResponse(suggestion.Category, suggestion.Confidence));
    }

    private static Error? Validate(SuggestCategoryQuery query)
    {
        if (query.Description is not null && query.Description.Trim().Length > Listing.MaxDescriptionLength)
            return Error.Validation("Listing.DescriptionTooLong",
                $"Description must have at most {Listing.MaxDescriptionLength} characters.");

        if (string.IsNullOrWhiteSpace(query.Title))
            return Error.Validation("Listing.TitleEmpty", "Title must not be empty.");
        if (query.Title.Trim().Length > Listing.MaxTitleLength)
            return Error.Validation("Listing.TitleTooLong",
                $"Title must have at most {Listing.MaxTitleLength} characters.");

        return null;
    }
}
