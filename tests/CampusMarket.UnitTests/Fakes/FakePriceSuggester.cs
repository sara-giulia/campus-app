using CampusMarket.Api.Common.Pricing;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePriceSuggester : IPriceSuggester
{
    public int SuggestedPrice { get; set; } = 123;

    public bool ThrowOnSuggest { get; set; }

    public int CallCount { get; private set; }

    public string? LastTitle { get; private set; }

    public string? LastDescription { get; private set; }

    public Category LastCategory { get; private set; }

    public Condition LastCondition { get; private set; }

    public int Suggest(string title, string? description, Category category, Condition condition)
    {
        CallCount++;
        LastTitle = title;
        LastDescription = description;
        LastCategory = category;
        LastCondition = condition;
        if (ThrowOnSuggest)
            throw new InvalidOperationException("Fake price suggester configured to throw.");
        return SuggestedPrice;
    }
}
