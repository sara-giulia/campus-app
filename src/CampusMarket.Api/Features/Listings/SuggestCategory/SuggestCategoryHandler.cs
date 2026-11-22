using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Features.Listings.SuggestCategory;

// Pure query: no state changes, no IUnitOfWork. The title and description are checked
// with Listing.ValidateText, the same rules and errors as when creating a listing.
// Below the minimum confidence (a setting chosen on validation data, see CategorySuggestionOptions)
// the suggestion is more likely wrong than useful, so no category is returned.
public sealed class SuggestCategoryHandler(ICategorySuggester suggester, IOptions<CategorySuggestionOptions> options)
    : IQueryHandler<SuggestCategoryQuery, SuggestCategoryResponse>
{
    public Task<Result<SuggestCategoryResponse>> HandleAsync(SuggestCategoryQuery query, CancellationToken ct)
    {
        var validation = Listing.ValidateText(query.Title, query.Description);
        if (validation is not null)
            return Task.FromResult(Result<SuggestCategoryResponse>.Failure(validation));

        var suggestion = suggester.Suggest(query.Title.Trim(), query.Description);
        var category = suggestion.Confidence >= options.Value.MinConfidence ? suggestion.Category : (Category?)null;
        return Task.FromResult<Result<SuggestCategoryResponse>>(
            new SuggestCategoryResponse(category, suggestion.Confidence));
    }
}
