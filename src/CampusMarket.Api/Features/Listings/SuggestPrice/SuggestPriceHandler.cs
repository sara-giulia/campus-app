using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Pricing;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.SuggestPrice;

// Pure query: no state changes, no IUnitOfWork. Validation rules and error codes
// mirror those used by Listing (title empty / too long, description too long,
// unknown category or condition) so a caller sees the same errors here as when
// creating a listing.
public sealed class SuggestPriceHandler(IPriceSuggester suggester)
    : IQueryHandler<SuggestPriceQuery, SuggestPriceResponse>
{
    public Task<Result<SuggestPriceResponse>> HandleAsync(SuggestPriceQuery query, CancellationToken ct)
    {
        var validation = Validate(query);
        if (validation is not null)
            return Task.FromResult(Result<SuggestPriceResponse>.Failure(validation));

        var suggested = suggester.Suggest(query.Title.Trim(), query.Description, query.Category, query.Condition);
        return Task.FromResult<Result<SuggestPriceResponse>>(new SuggestPriceResponse(suggested));
    }

    private static Error? Validate(SuggestPriceQuery query)
    {
        if (!Enum.IsDefined(query.Category))
            return Error.Validation("Listing.CategoryInvalid", "Unknown category.");
        if (!Enum.IsDefined(query.Condition))
            return Error.Validation("Listing.ConditionInvalid", "Unknown condition.");

        return Listing.ValidateText(query.Title, query.Description);
    }
}
