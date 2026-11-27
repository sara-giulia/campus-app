using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Similarity;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Listings.GetSimilarListings;

// Pure query: the listing (any status, like GET /listings/{id}) and every other Active listing,
// ranked by the similarity the model gives to their title and description; the five closest come
// back. Equal similarities: the newest first, then by id, so the answer does not change between calls.
// A listing of a suspended member is "not found" (like GET /listings/{id}) and never suggested.
public sealed class GetSimilarListingsHandler(
    IListingRepository repository, ISimilarListingsFinder finder, IMemberBlockRepository blocks,
    IMemberRepository members, TimeProvider timeProvider)
    : IQueryHandler<GetSimilarListingsQuery, GetSimilarListingsResponse>
{
    public async Task<Result<GetSimilarListingsResponse>> HandleAsync(
        GetSimilarListingsQuery query, CancellationToken ct)
    {
        var target = await repository.GetByIdAsync(query.Id, ct);
        var suspendedIds = await members.GetSuspendedIdsAsync(timeProvider.GetUtcNow(), ct);
        if (target is null || suspendedIds.Contains(target.SellerId))
            return Result<GetSimilarListingsResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{query.Id}' was not found."));

        // The listings of the members the viewer blocked are not suggested.
        var blockedIds = query.ViewerId is { } viewerId
            ? await blocks.GetBlockedIdsAsync(viewerId, ct)
            : new HashSet<Guid>();

        var candidates = (await repository.GetAllAsync(ct))
            .Where(l => l.Status == ListingStatus.Active && l.Id != target.Id
                        && !blockedIds.Contains(l.SellerId) && !suspendedIds.Contains(l.SellerId))
            .ToList();

        if (candidates.Count == 0)
            return new GetSimilarListingsResponse([]);

        var similarities = finder.Similarities(
            new ListingText(target.Title, target.Description),
            candidates.Select(c => new ListingText(c.Title, c.Description)).ToList());

        var items = candidates
            .Select((listing, i) => (Listing: listing, Similarity: similarities[i]))
            .OrderByDescending(x => x.Similarity)
            .ThenByDescending(x => x.Listing.CreatedAt)
            .ThenBy(x => x.Listing.Id)
            .Take(GetSimilarListingsQuery.MaxResults)
            .Select(x => new SimilarListingItem(
                x.Listing.Id,
                x.Listing.Title,
                x.Listing.Category,
                x.Listing.Condition,
                x.Listing.Price.Amount,
                Math.Round(x.Similarity, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new GetSimilarListingsResponse(items);
    }
}
