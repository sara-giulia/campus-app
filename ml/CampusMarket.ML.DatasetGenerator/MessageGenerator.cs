using System.Globalization;
using System.Text;

namespace CampusMarket.ML.DatasetGenerator;

public sealed record MessageRow(string Text, bool IsSpam, string TemplateId);

// The chat messages of ml/data/messages.csv (the spam model of the messages). Every row picks a template,
// one of its phrasings and fills the {slots}, then gets the noise of real chat: no diacritics, lower case,
// short forms, a typo, an emoji, a greeting. Its own Random, so listings.csv stays byte for byte the same.
public sealed class MessageGenerator(Random rng)
{
    public const int TotalRows = 5000;
    public const double SpamRate = 0.15;
    public const int MaxTextLength = 1000;

    private const double StripDiacriticsRate = 0.55;
    private const double LowerCaseRate = 0.30;
    private const double AbbreviationRate = 0.30;
    private const double TypoRate = 0.15;
    private const double EmojiRate = 0.10;
    private const double GreetingRate = 0.30;
    private const double ClosingRate = 0.20;
    private const double LoudSpamRate = 0.10;

    private static readonly string[] Greetings = ["Bună,", "Salut,", "Salut!", "Hei,", "Bună ziua,", "Bună seara,"];
    private static readonly string[] NormalClosings = ["Mersi!", "Aștept răspuns.", "Mulțumesc!", "Zi-mi când poți.", "Ms."];
    private static readonly string[] SpamClosings = ["Scrie-mi repede!", "Locuri limitate!", "Nu rata ocazia!", "Ofertă valabilă azi.", "Răspunde urgent."];
    private static readonly string[] NormalEmojis = ["🙂", "👍", "😊", "🙏", "😅"];
    private static readonly string[] SpamEmojis = ["🔥", "💰", "✅", "🎁", "💸"];

    private static readonly (string Pattern, string Replacement)[] Abbreviations =
    [
        ("Mersi", "Ms"), ("mersi", "ms"), ("pentru", "pt"), ("foarte", "f"), ("te rog", "te rog frumos"),
        ("Mulțumesc", "Mulțumesc mult"), ("bine", "bn"), ("acum", "acm"), ("nu știu", "nush"), ("Bună", "Buna"),
    ];

    public IEnumerable<MessageRow> Generate()
    {
        var spamCount = (int)Math.Round(TotalRows * SpamRate);
        for (var i = 0; i < TotalRows; i++)
        {
            // Spam rows spread over the file instead of all at the end.
            var isSpam = i % (TotalRows / spamCount) == 0 && i / (TotalRows / spamCount) < spamCount;
            yield return isSpam ? Build(MessageTemplates.Spam) : Build(MessageTemplates.Normal);
        }
    }

    private MessageRow Build(MessageTemplate[] templates)
    {
        var template = templates[rng.Next(templates.Length)];
        var text = Fill(template.Phrasings[rng.Next(template.Phrasings.Length)]);

        if (rng.NextDouble() < GreetingRate)
            text = Pick(Greetings) + " " + LowerFirst(text);
        if (rng.NextDouble() < ClosingRate)
            text += " " + Pick(template.IsSpam ? SpamClosings : NormalClosings);
        if (template.IsSpam && rng.NextDouble() < LoudSpamRate)
            text = text.ToUpperInvariant() + "!!!";

        if (rng.NextDouble() < StripDiacriticsRate)
            text = StripDiacritics(text);
        if (rng.NextDouble() < LowerCaseRate)
            text = text.ToLowerInvariant();
        if (rng.NextDouble() < AbbreviationRate)
            text = Abbreviate(text);
        if (rng.NextDouble() < TypoRate)
            text = Typo(text);
        if (rng.NextDouble() < EmojiRate)
            text += " " + Pick(template.IsSpam ? SpamEmojis : NormalEmojis);

        return new MessageRow(text.Length > MaxTextLength ? text[..MaxTextLength] : text, template.IsSpam, template.Id);
    }

    private string Fill(string phrasing)
    {
        var price = rng.Next(1, 90) * 10;
        return phrasing
            .Replace("{item}", Pick(MessageTemplates.Items), StringComparison.Ordinal)
            .Replace("{priceHigh}", (price * rng.Next(2, 4)).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{price}", price.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{place}", Pick(MessageTemplates.Places), StringComparison.Ordinal)
            .Replace("{time}", Pick(MessageTemplates.Times), StringComparison.Ordinal)
            .Replace("{day}", Pick(MessageTemplates.Days), StringComparison.Ordinal)
            .Replace("{dorm}", "C" + rng.Next(1, 13).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{room}", rng.Next(1, 450).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{yearFull}", rng.Next(2015, 2027).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{year}", rng.Next(1, 5).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{hours}", rng.Next(2, 13).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{subject}", Pick(MessageTemplates.Subjects), StringComparison.Ordinal)
            .Replace("{faculty}", Pick(MessageTemplates.Faculties), StringComparison.Ordinal)
            .Replace("{phone}", Phone(), StringComparison.Ordinal)
            .Replace("{email}", Email(), StringComparison.Ordinal)
            .Replace("{amount}", (rng.Next(1, 100) * 50).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{percent}", (rng.Next(2, 31) * 10).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{link}", Link(), StringComparison.Ordinal)
            .Replace("{handle}", "@" + Pick(MessageTemplates.HandleWords) + rng.Next(10, 999).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{code}", Code(), StringComparison.Ordinal);
    }

    private string Phone()
    {
        var digits = "07" + rng.Next(20, 90).ToString(CultureInfo.InvariantCulture)
                          + rng.Next(100, 1000).ToString(CultureInfo.InvariantCulture)
                          + rng.Next(100, 1000).ToString(CultureInfo.InvariantCulture);
        return rng.Next(3) switch
        {
            0 => digits,
            1 => $"{digits[..4]} {digits[4..7]} {digits[7..]}",
            _ => $"{digits[..4]}.{digits[4..7]}.{digits[7..]}",
        };
    }

    private string Email() =>
        $"{Pick(MessageTemplates.Names)}.{Pick(MessageTemplates.Names)}{rng.Next(1, 99).ToString(CultureInfo.InvariantCulture)}@student.uaic.ro";

    private string Link()
    {
        var host = $"{Pick(MessageTemplates.LinkWords)}-{Pick(MessageTemplates.LinkWords)}.{Pick(MessageTemplates.LinkTlds)}";
        return rng.Next(3) switch
        {
            0 => "https://" + host + "/" + Code(),
            1 => "www." + host,
            _ => host,
        };
    }

    private string Code()
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyz23456789";
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[rng.Next(alphabet.Length)];
        return new string(chars);
    }

    private string Pick(string[] values) => values[rng.Next(values.Length)];

    private static string LowerFirst(string text) =>
        text.Length > 1 && char.IsUpper(text[0]) && char.IsLower(text[1]) ? char.ToLowerInvariant(text[0]) + text[1..] : text;

    private string Abbreviate(string text)
    {
        foreach (var (pattern, replacement) in Abbreviations)
        {
            if (rng.NextDouble() < 0.6)
                text = text.Replace(pattern, replacement, StringComparison.Ordinal);
        }
        return text;
    }

    private string Typo(string text)
    {
        if (text.Length < 4)
            return text;
        var chars = text.ToCharArray();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var i = rng.Next(chars.Length - 1);
            if (char.IsLetter(chars[i]) && char.IsLetter(chars[i + 1]))
            {
                (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
                return new string(chars);
            }
        }
        return text;
    }

    private static string StripDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark))
            sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
