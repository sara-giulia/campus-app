using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

// How a listing was found by the search: its title contains the searched words (Text), or only its
// meaning is close to the search (Meaning, from ISemanticSearch).
public enum SearchMatch { Text, Meaning }

// The search of GET /listings: first the listings whose title contains the search (case and diacritics
// ignored, the newest first, as before), then the ones found only by meaning, the closest first.
// The candidates are already filtered (Active, category, condition, price): a match by meaning outside
// the filters is dropped. Used by GetListingsHandler and by the evaluation in ML.Tests.
public static class ListingSearch
{
    // Shorter searches have too little meaning for the model ("pc", "a"): words only.
    public const int MinLengthForMeaning = 3;

    public static IReadOnlyList<(Listing Listing, SearchMatch Match)> Rank(
        IEnumerable<Listing> candidates, string search, IReadOnlyList<SemanticMatch> meaning)
    {
        var folded = SearchText.Fold(search.Trim());
        var list = candidates as IReadOnlyCollection<Listing> ?? candidates.ToList();

        var byText = list
            .Where(l => SearchText.Fold(l.Title).Contains(folded, StringComparison.Ordinal))
            .OrderByDescending(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .ToList();
        var textIds = byText.Select(l => l.Id).ToHashSet();

        var byId = list.ToDictionary(l => l.Id);
        var byMeaning = meaning
            .Where(m => !textIds.Contains(m.ListingId) && byId.ContainsKey(m.ListingId))
            .Select(m => byId[m.ListingId]);

        return [.. byText.Select(l => (l, SearchMatch.Text)), .. byMeaning.Select(l => (l, SearchMatch.Meaning))];
    }
}
