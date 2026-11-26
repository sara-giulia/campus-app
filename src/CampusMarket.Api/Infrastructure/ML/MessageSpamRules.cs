using System.Text.RegularExpressions;

namespace CampusMarket.Api.Infrastructure.ML;

// Clear signs of a scam in a chat message that the model can miss on a short text: a link, card data,
// a code from an SMS or from the bank, money asked in advance, or the usual bait (easy money, crypto,
// loans, gift cards). Unlike SpamRules of the listings, a phone number, a WhatsApp or an e-mail address
// is not a sign here: two students who agree to meet share them all the time. A rule only shows the
// message with a warning, it never blocks it: only the model blocks, when it is very sure.
public static partial class MessageSpamRules
{
    private static readonly string[] Phrases =
    [
        "bani rapid", "bani usor", "profit garantat", "castig garantat", "castigi zilnic", "fara risc",
        "bitcoin", "crypto", "credit rapid", "imprumut rapid", "card cadou", "carduri cadou", "gift card",
        "bonus la prima depunere", "datele cardului", "numarul cardului", "data expirarii", "poza cu cardul",
        "enter your card", "card details", "datele de logare",
    ];

    public static bool Matches(string text)
    {
        // An e-mail address (ana.pop@student.ro) is a normal way to reach a colleague, not a link.
        var folded = Email().Replace(SpamRules.Normalize(text), " ");
        return Phrases.Any(folded.Contains)
               || Link().IsMatch(folded)
               || SpelledLink().IsMatch(folded)
               || Cvv().IsMatch(folded)
               || CodeFromSmsOrBank().IsMatch(folded)
               || MoneyInAdvance().IsMatch(folded);
    }

    [GeneratedRegex(@"[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Email();

    // http://…, www.…, or a word ending in a common domain (plata-sigura.com, bit.ly/x).
    [GeneratedRegex(@"https?://|www\.|\b[a-z0-9-]+\.(ro|com|net|org|info|biz|xyz|io|me|ly|online|site|link|top)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Link();

    // "bani-rapizi punct ro": a link written so that it does not look like one.
    [GeneratedRegex(@"\bpunct (ro|com|net|org)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex SpelledLink();

    [GeneratedRegex(@"\bcvv\b|\bcvc\b|cifre din spate|codul de pe spate", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Cvv();

    // "trimite-mi codul primit prin SMS", "spune-i codul de la banca"; not "codul de la interfon".
    [GeneratedRegex(@"\bcod(ul)?\b.{0,40}\b(sms|primit|banca|bancii|confirm\w*)\b|\b(sms|banca|bancii)\b.{0,40}\bcod(ul)?\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex CodeFromSmsOrBank();

    // "trimite 150 lei avans", "plata in avans", "platesti inainte"; not "nu cer avans".
    [GeneratedRegex(@"\b(trimit|plates|vireaz|transfer|send|pay)\w*\b.{0,40}\bavans|\bin avans\b|\bplata (inainte|anticipat)|\bplatesti (acum|inainte)\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex MoneyInAdvance();
}
