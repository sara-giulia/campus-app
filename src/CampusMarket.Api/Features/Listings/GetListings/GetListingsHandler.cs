using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

// With a search, the listings found by words come first and then the ones found by meaning
// (ListingSearch); "relevance" (the default when searching) keeps that order, the other sorts reorder
// all of them. Without a search the default stays "newest".
public sealed class GetListingsHandler(
    IListingRepository repository, ISemanticSearch semanticSearch, IMemberBlockRepository blocks)
    : IQueryHandler<GetListingsQuery, GetListingsResponse>
{
    public async Task<Result<GetListingsResponse>> HandleAsync(
        GetListingsQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<GetListingsResponse>.Failure(
                Error.Validation("Listings.InvalidPage", "Page must be at least 1."));
        if (query.PageSize < 1 || query.PageSize > GetListingsQuery.MaxPageSize)
            return Result<GetListingsResponse>.Failure(
                Error.Validation("Listings.InvalidPageSize",
                    $"Page size must be between 1 and {GetListingsQuery.MaxPageSize}."));
        if (query.MinPrice > query.MaxPrice)
            return Result<GetListingsResponse>.Failure(
                Error.Validation("Listings.InvalidPriceRange",
                    "Minimum price cannot be greater than maximum price."));
        if (query.Sort is not null && !GetListingsQuery.AllowedSorts.Contains(query.Sort))
            return Result<GetListingsResponse>.Failure(
                Error.Validation("Listings.InvalidSort", "Sort must be relevance, newest, price_asc or price_desc."));

        var listings = await repository.GetAllAsync(ct);
        IEnumerable<Listing> filtered = listings.Where(l => l.Status == ListingStatus.Active);

        if (query.ViewerId is { } viewerId)
        {
            var blockedIds = await blocks.GetBlockedIdsAsync(viewerId, ct);
            filtered = filtered.Where(l => !blockedIds.Contains(l.SellerId));
        }

        if (query.Category is not null)
            filtered = filtered.Where(l => l.Category == query.Category);
        if (query.Condition is not null)
            filtered = filtered.Where(l => l.Condition == query.Condition);
        if (query.MinPrice is not null)
            filtered = filtered.Where(l => l.Price.Amount >= query.MinPrice);
        if (query.MaxPrice is not null)
            filtered = filtered.Where(l => l.Price.Amount <= query.MaxPrice);

        var found = await SearchAsync(filtered, query.Search?.Trim(), ct);

        var sorted = (query.Sort switch
        {
            "price_asc" => found.OrderBy(f => f.Listing.Price.Amount).ThenBy(f => f.Listing.Id),
            "price_desc" => found.OrderByDescending(f => f.Listing.Price.Amount).ThenBy(f => f.Listing.Id),
            "newest" => found.OrderByDescending(f => f.Listing.CreatedAt).ThenBy(f => f.Listing.Id),
            // relevance, or no sort: the order of SearchAsync
            _ => found.AsEnumerable()
        }).ToList();

        var items = sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(f => GetListingsItem.From(f.Listing, f.Match))
            .ToList();

        return new GetListingsResponse(items, items.Count, sorted.Count, query.Page, query.PageSize);
    }

    // Without a search: every filtered listing, the newest first, with no Match.
    private async Task<IReadOnlyList<(Listing Listing, SearchMatch? Match)>> SearchAsync(
        IEnumerable<Listing> filtered, string? search, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(search))
            return filtered
                .OrderByDescending(l => l.CreatedAt)
                .ThenBy(l => l.Id)
                .Select(l => (l, (SearchMatch?)null))
                .ToList();

        IReadOnlyList<SemanticMatch> meaning = search.Length >= ListingSearch.MinLengthForMeaning
            ? await semanticSearch.FindAsync(search, ct)
            : [];

        return ListingSearch.Rank(filtered, search, meaning)
            .Select(r => (r.Listing, (SearchMatch?)r.Match))
            .ToList();
    }
}
