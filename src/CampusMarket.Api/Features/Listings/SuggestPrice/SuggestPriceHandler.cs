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
        var required = Listing.RequireClassification(query.Category, query.Condition);
        if (required.IsFailure)
            return Task.FromResult(Result<SuggestPriceResponse>.Failure(required.Error));
        var (category, condition) = required.Value;

        var validation = Validate(query, category, condition);
        if (validation is not null)
            return Task.FromResult(Result<SuggestPriceResponse>.Failure(validation));

        var suggested = suggester.Suggest(query.Title.Trim(), query.Description, category, condition);
        return Task.FromResult<Result<SuggestPriceResponse>>(new SuggestPriceResponse(suggested));
    }

    private static Error? Validate(SuggestPriceQuery query, Category category, Condition condition)
    {
        if (!Enum.IsDefined(category))
            return Error.Validation("Listing.CategoryInvalid", "Unknown category.");
        if (!Enum.IsDefined(condition))
            return Error.Validation("Listing.ConditionInvalid", "Unknown condition.");

        return Listing.ValidateText(query.Title, query.Description);
    }
}
