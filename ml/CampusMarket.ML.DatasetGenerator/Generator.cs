using System.Globalization;
using System.Text;

namespace CampusMarket.ML.DatasetGenerator;

public sealed record Row(
    string Title,
    string Description,
    Category Category,
    Condition Condition,
    int Price,
    bool IsSpam,
    string TemplateId);

// Dataset v2. Reads the full item catalog from JSON (see Catalog.LoadAll) and layers on top of it
// the kinds of noise a scraped student marketplace would actually contain: stripped diacritics,
// typos, abbreviations, casing changes, emojis, missing or very long descriptions, spam that does
// not shout, label-noise where the seller picked the wrong category, and prices that drift. The
// rng is always the seeded Random from Program.cs so two runs produce identical rows.
public sealed class Generator
{
    public const int TotalRows = 5000;
    public const int MaxTitleLength = 100;
    public const int MinTitleLength = 3;
    public const int MaxDescriptionLength = 1000;
    public const double SpamRate = 0.10;
    public const double DisguisedSpamShare = 1.0 / 3.0;
    public const double WrongCategoryRate = 0.03;
    public const double EmptyDescriptionRate = 0.12;
    public const double LongDescriptionRate = 0.08;
    public const double StripDiacriticsRate = 0.55;
    public const double RandomCapsRate = 0.10;
    public const double AllCapsRate = 0.04;
    public const double EmojiRate = 0.12;
    public const double TypoRate = 0.18;
    public const double AbbreviationRate = 0.35;
    public const double NormalWithSpamyWordRate = 0.06;
    public const double NewPatternShare = 0.5;

    private static readonly string[] Emojis =
    {
        "📚", "💻", "📱", "🎧", "🔬", "⚡", "🔥", "✨", "👌", "🎓", "🏷️", "💡", "🛒", "📖",
    };

    private static readonly (string Pattern, string Replacement)[] Abbreviations =
    {
        ("vand ", "vand "),
        ("Vand ", "Vand "),
        ("stare foarte buna", "stare f buna"),
        ("Stare foarte buna", "Stare f buna"),
        ("pentru", "pt"),
        ("Pentru", "Pt"),
        ("negociabil", "neg."),
        ("Negociabil", "Neg."),
        ("semestru", "sem"),
        ("semestrul", "sem"),
        ("editie", "ed."),
        ("volum", "vol."),
        ("ca nou", "ca nou, impecabil"),
    };

    private static readonly string[] SpamyWordsForNormal =
    {
        "urgent", "promotie", "pret final", "rapid", "nu ratati", "oferta",
    };

    private readonly Random _rng;
    private readonly Random _spamRng;
    private readonly ItemTemplate[] _items;
    private readonly Dictionary<Category, ItemTemplate[]> _byCategory;
    private readonly Category[] _allCategories;

    // rng drives every row; spamRng only decides the pattern and the text of spam rows, so adding
    // spam patterns does not move the main stream that the non-spam rows are drawn from.
    public Generator(Random rng, Random spamRng, IReadOnlyList<ItemTemplate> items)
    {
        _rng = rng;
        _spamRng = spamRng;
        _items = items.ToArray();
        _byCategory = _items
            .GroupBy(i => i.Category)
            .ToDictionary(g => g.Key, g => g.ToArray());
        _allCategories = _byCategory.Keys.OrderBy(c => (int)c).ToArray();
    }

    public IReadOnlyDictionary<Category, int> ItemTypeCountPerCategory() =>
        _byCategory.ToDictionary(kv => kv.Key, kv => kv.Value.Length);

    public IEnumerable<Row> Generate()
    {
        var spamCount = (int)Math.Round(TotalRows * SpamRate);
        var normalCount = TotalRows - spamCount;

        var kinds = new List<int>(TotalRows);
        kinds.AddRange(Enumerable.Repeat(0, normalCount));
        kinds.AddRange(Enumerable.Repeat(1, spamCount));
        Shuffle(kinds, _rng);

        foreach (var kind in kinds)
        {
            yield return kind == 0 ? GenerateNormal() : GenerateSpam();
        }
    }

    private Row GenerateNormal()
    {
        var item = _items[_rng.Next(_items.Length)];
        var title = PickTitle(item);
        var condition = PickCondition();
        var description = BuildDescription(item.DescriptionLines);
        var price = ComputePrice(item.BasePriceMin, item.BasePriceMax, condition);

        if (_rng.NextDouble() < NormalWithSpamyWordRate)
        {
            description = SprinkleSpamyWord(description);
        }

        var label = item.Category;
        if (_rng.NextDouble() < WrongCategoryRate)
        {
            label = PickDifferentCategory(item.Category);
        }

        title = DecorateTitle(title);
        description = DecorateDescription(description, _rng);

        return new Row(
            Title: ClampTitle(title),
            Description: ClampDescription(description),
            Category: label,
            Condition: condition,
            Price: price,
            IsSpam: false,
            TemplateId: item.Id);
    }

    // The main stream (_rng) makes exactly the draws of the original generator, in the same order,
    // so every non-spam row is unchanged. Which pattern the row finally uses, and the text of the
    // new patterns, come from _spamRng, which only spam rows touch. Every spam row, loud or
    // disguised, gets its pattern as TemplateId, so a pattern's texts stay in one part of the split.
    private Row GenerateSpam()
    {
        var disguised = _rng.NextDouble() < DisguisedSpamShare;

        if (disguised)
        {
            var item = _items[_rng.Next(_items.Length)];
            var title = PickTitle(item);
            var condition = PickCondition();
            var (description, legacyIndex) = Spam.PickDescriptionWithIndex(_rng);
            var price = ComputePrice(item.BasePriceMin, item.BasePriceMax, condition);

            title = DecorateTitle(title);
            description = DecorateDescription(description, _rng, allowAbbrev: false);

            var pattern = PickPattern(legacyIndex);
            if (IsNewPattern(pattern))
            {
                price = SpamPriceFor(pattern, price, disguised: true);
                var template = pattern.Descriptions[_spamRng.Next(pattern.Descriptions.Length)];
                description = DecorateDescription(Spam.Fill(template, _spamRng, price), _spamRng, allowAbbrev: false);
            }

            return new Row(
                Title: ClampTitle(title),
                Description: ClampDescription(description),
                Category: item.Category,
                Condition: condition,
                Price: price,
                IsSpam: true,
                TemplateId: pattern.Id);
        }
        else
        {
            // Drawn only to keep the main stream aligned; the title now belongs to the pattern.
            _ = Spam.PickTitle(_rng);
            var condition = PickCondition();
            var (description, legacyIndex) = Spam.PickDescriptionWithIndex(_rng);

            int price;
            var trick = _rng.Next(3);
            if (trick == 0)
            {
                price = 1;
            }
            else if (trick == 1)
            {
                price = _rng.Next(50000, 100000);
            }
            else
            {
                price = _rng.Next(5, 50);
            }

            var pattern = PickPattern(legacyIndex);
            var title = pattern.Titles[_spamRng.Next(pattern.Titles.Length)];
            if (IsNewPattern(pattern))
            {
                price = SpamPriceFor(pattern, price, disguised: false);
                var template = pattern.Descriptions[_spamRng.Next(pattern.Descriptions.Length)];
                description = Spam.Fill(template, _spamRng, price);
            }

            return new Row(
                Title: ClampTitle(title),
                Description: ClampDescription(description),
                Category: Category.Other,
                Condition: condition,
                Price: price,
                IsSpam: true,
                TemplateId: pattern.Id);
        }
    }

    // About half of the spam keeps its original pattern (spam-00 ... spam-11, the text already
    // drawn on the main stream); the rest gets one of the new patterns. Always two draws.
    private SpamPattern PickPattern(int legacyIndex)
    {
        var useNew = _spamRng.NextDouble() < NewPatternShare;
        var newIndex = _spamRng.Next(Spam.New.Count);
        return useNew ? Spam.New[newIndex] : Spam.Patterns[legacyIndex];
    }

    private static bool IsNewPattern(SpamPattern pattern) => Spam.New.Contains(pattern);

    // Absurd keeps the price already drawn (loud: 1 leu, 5-49 or 50000-99999; disguised: the
    // item's price). TooCheap is 10-25% of the item's price, or 49-399 lei for a loud row.
    // Plausible keeps the item's price, or 20-299 lei for a loud row.
    private int SpamPriceFor(SpamPattern pattern, int drawnPrice, bool disguised)
    {
        return pattern.Price switch
        {
            SpamPrice.TooCheap when disguised =>
                Math.Max(1, (int)Math.Round(drawnPrice * (0.10 + _spamRng.NextDouble() * 0.15), MidpointRounding.AwayFromZero)),
            SpamPrice.TooCheap => _spamRng.Next(49, 400),
            SpamPrice.Plausible when !disguised => _spamRng.Next(20, 300),
            _ => drawnPrice,
        };
    }

    private string PickTitle(ItemTemplate item) =>
        item.Titles[_rng.Next(item.Titles.Length)];

    private Condition PickCondition()
    {
        var roll = _rng.NextDouble();
        return roll switch
        {
            < 0.25 => Condition.New,
            < 0.75 => Condition.Good,
            _ => Condition.Worn,
        };
    }

    private Category PickDifferentCategory(Category exclude)
    {
        while (true)
        {
            var pick = _allCategories[_rng.Next(_allCategories.Length)];
            if (pick != exclude)
            {
                return pick;
            }
        }
    }

    private string BuildDescription(string[] source)
    {
        var roll = _rng.NextDouble();
        if (roll < EmptyDescriptionRate)
        {
            return string.Empty;
        }

        int sentences;
        if (roll < EmptyDescriptionRate + LongDescriptionRate)
        {
            sentences = 4 + _rng.Next(3);
        }
        else
        {
            sentences = 1 + _rng.Next(3);
        }

        sentences = Math.Min(sentences, source.Length);
        var picked = new HashSet<int>();
        var parts = new List<string>(sentences);
        while (parts.Count < sentences)
        {
            var index = _rng.Next(source.Length);
            if (picked.Add(index))
            {
                parts.Add(source[index]);
            }
        }
        return string.Join(" ", parts);
    }

    private string SprinkleSpamyWord(string description)
    {
        var word = SpamyWordsForNormal[_rng.Next(SpamyWordsForNormal.Length)];
        return string.IsNullOrEmpty(description) ? word : description + " " + word + ".";
    }

    private int ComputePrice(int basePriceMin, int basePriceMax, Condition condition)
    {
        var baseValue = basePriceMin + _rng.NextDouble() * (basePriceMax - basePriceMin);
        var multiplier = condition switch
        {
            Condition.New => 1.0,
            Condition.Good => 0.7,
            Condition.Worn => 0.45,
            _ => 1.0,
        };
        var noise = 0.80 + _rng.NextDouble() * 0.40;
        var price = (int)Math.Round(baseValue * multiplier * noise, MidpointRounding.AwayFromZero);
        return Math.Max(1, price);
    }

    private string DecorateTitle(string title)
    {
        var text = title;
        if (_rng.NextDouble() < StripDiacriticsRate)
        {
            text = StripDiacritics(text);
        }
        if (_rng.NextDouble() < TypoRate)
        {
            text = IntroduceTypo(text, _rng);
        }
        if (_rng.NextDouble() < AbbreviationRate)
        {
            text = ApplyAbbreviations(text, _rng);
        }
        var capsRoll = _rng.NextDouble();
        if (capsRoll < AllCapsRate)
        {
            text = text.ToUpperInvariant();
        }
        else if (capsRoll < AllCapsRate + RandomCapsRate)
        {
            text = RandomizeCaps(text);
        }
        if (_rng.NextDouble() < EmojiRate)
        {
            text = AddEmoji(text, _rng);
        }
        return text;
    }

    private static string DecorateDescription(string description, Random rng, bool allowAbbrev = true)
    {
        if (string.IsNullOrEmpty(description))
        {
            return description;
        }
        var text = description;
        if (rng.NextDouble() < StripDiacriticsRate)
        {
            text = StripDiacritics(text);
        }
        if (allowAbbrev && rng.NextDouble() < AbbreviationRate)
        {
            text = ApplyAbbreviations(text, rng);
        }
        if (rng.NextDouble() < TypoRate)
        {
            text = IntroduceTypo(text, rng);
        }
        if (rng.NextDouble() < EmojiRate)
        {
            text = AddEmoji(text, rng);
        }
        return text;
    }

    private static string StripDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('ș', 's')
            .Replace('Ș', 'S')
            .Replace('ț', 't')
            .Replace('Ț', 'T');
    }

    private static string ApplyAbbreviations(string value, Random rng)
    {
        var text = value;
        foreach (var (pattern, replacement) in Abbreviations)
        {
            if (rng.NextDouble() < 0.6)
            {
                text = text.Replace(pattern, replacement, StringComparison.Ordinal);
            }
        }
        return text;
    }

    private static string IntroduceTypo(string value, Random rng)
    {
        if (value.Length < 4)
        {
            return value;
        }
        var chars = value.ToCharArray();
        var attempts = 0;
        while (attempts++ < 6)
        {
            var idx = rng.Next(chars.Length - 1);
            if (char.IsLetter(chars[idx]) && char.IsLetter(chars[idx + 1]) && chars[idx] != ' ')
            {
                (chars[idx], chars[idx + 1]) = (chars[idx + 1], chars[idx]);
                return new string(chars);
            }
        }
        return value;
    }

    private string RandomizeCaps(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i]))
            {
                continue;
            }
            chars[i] = _rng.NextDouble() < 0.5
                ? char.ToUpperInvariant(chars[i])
                : char.ToLowerInvariant(chars[i]);
        }
        return new string(chars);
    }

    private static string AddEmoji(string value, Random rng)
    {
        var emoji = Emojis[rng.Next(Emojis.Length)];
        return rng.NextDouble() < 0.5 ? emoji + " " + value : value + " " + emoji;
    }

    private static string ClampTitle(string title)
    {
        if (title.Length > MaxTitleLength)
        {
            title = title[..MaxTitleLength];
        }
        if (title.Length < MinTitleLength)
        {
            title = title.PadRight(MinTitleLength, '.');
        }
        return title;
    }

    private static string ClampDescription(string description)
    {
        if (description.Length > MaxDescriptionLength)
        {
            description = description[..MaxDescriptionLength];
        }
        return description;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
