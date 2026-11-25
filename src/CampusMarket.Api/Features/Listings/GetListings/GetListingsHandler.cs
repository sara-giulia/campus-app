using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

public sealed class GetListingsHandler(IListingRepository repository, IMemberBlockRepository blocks)
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
                Error.Validation("Listings.InvalidSort", "Sort must be newest, price_asc or price_desc."));

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
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(l =>
                l.Title.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

        var ordered = (
            // Stryker disable once String : the switch has no "newest" arm, so null, "newest" and "" all fall into the default arm and the fallback value is never observable
            query.Sort ?? "newest") switch
        {
            "price_asc" => filtered.OrderBy(l => l.Price.Amount).ThenBy(l => l.Id),
            "price_desc" => filtered.OrderByDescending(l => l.Price.Amount).ThenBy(l => l.Id),
            _ => filtered.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id)
        };
        var sorted = ordered.ToList();

        var items = sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(GetListingsItem.From)
            .ToList();

        return new GetListingsResponse(items, items.Count, sorted.Count, query.Page, query.PageSize);
    }
}
