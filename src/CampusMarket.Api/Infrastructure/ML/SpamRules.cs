using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CampusMarket.Api.Infrastructure.ML;

// Clear signs of spam that the model can miss on short texts it has never seen: a link, a phone number,
// asking to talk outside the app, "!!!", or the usual money bait and pay-in-advance phrases. A listing
// that matches one of them is spam whatever the model says (SpamDetector). The text is compared in
// lower case and without diacritics, so "Câștigă" and "castiga" are the same.
public static partial class SpamRules
{
    // What the listing is marked with when only a rule caught it: high, so the admin sees it as spam.
    public const double RuleProbability = 0.9;

    private static readonly string[] Phrases =
    [
        "whatsapp", "telegram", "viber", "signal me", "scrie-mi pe", "scrie pe", "contact pe", "in bio", "din bio",
        "bani rapid", "bani usor", "castig", "castiga", "profit garantat", "fara investitie", "investitie minima",
        "plata in avans", "plata anticipat", "transfer in avans", "crypto", "bitcoin", "credit rapid", "imprumut rapid",
        "cumpar conturi", "vand conturi", "click aici", "link in", "oferta limitata", "doar azi",
    ];

    public static bool Matches(string title, string? description)
    {
        // An email address (ana.pop@student.ro) is a normal way to reach a colleague, not a link.
        var text = Email().Replace(Normalize($"{title} {description}"), " ");
        return Phrases.Any(text.Contains)
               || Link().IsMatch(text)
               || Phone().IsMatch(text)
               || Handle().IsMatch(text)
               || text.Contains("!!!", StringComparison.Ordinal);
    }

    private static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            builder.Append(c);
        return builder.ToString();
    }

    [GeneratedRegex(@"[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Email();

    // http://…, www.…, or a word ending in a common domain (bani-usori.ro, oferta.com).
    [GeneratedRegex(@"https?://|www\.|\b[a-z0-9-]+\.(ro|com|net|org|info|biz|xyz|io|me)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Link();

    // A Romanian mobile number: 07xx xxx xxx (spaces, dots or dashes allowed), or +40 7…
    [GeneratedRegex(@"(\+40|\b0)7\d{2}[ .-]?\d{3}[ .-]?\d{3}\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Phone();

    // An @handle (Telegram, Instagram), but not an email address (nothing right before the @).
    [GeneratedRegex(@"(^|\s)@[a-z0-9_]{3,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Handle();
}
