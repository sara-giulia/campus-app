using CampusMarket.Api.Common.Categories;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.SuggestCategory;

// Pure query: no state changes, no IUnitOfWork. The title and description are checked
// with Listing.ValidateText, the same rules and errors as when creating a listing.
public sealed class SuggestCategoryHandler(ICategorySuggester suggester)
    : IQueryHandler<SuggestCategoryQuery, SuggestCategoryResponse>
{
    public Task<Result<SuggestCategoryResponse>> HandleAsync(SuggestCategoryQuery query, CancellationToken ct)
    {
        var validation = Listing.ValidateText(query.Title, query.Description);
        if (validation is not null)
            return Task.FromResult(Result<SuggestCategoryResponse>.Failure(validation));

        var suggestion = suggester.Suggest(query.Title.Trim(), query.Description);
        return Task.FromResult<Result<SuggestCategoryResponse>>(
            new SuggestCategoryResponse(suggestion.Category, suggestion.Confidence));
    }
}
