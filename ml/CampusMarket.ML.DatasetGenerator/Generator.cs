using System.Globalization;

namespace CampusMarket.ML.DatasetGenerator;

public sealed record Row(
    string Title,
    string Description,
    Category Category,
    Condition Condition,
    int Price,
    bool IsSpam);

public static class Generator
{
    public const int TotalRows = 3000;
    public const int MaxTitleLength = 100;
    public const int MinTitleLength = 3;
    public const int MaxDescriptionLength = 1000;
    public const double SpamRate = 0.10;
    public const double HardExamplesRate = 0.10;
    public const double EmptyDescriptionRate = 0.15;
    public const double DisguisedSpamShare = 1.0 / 3.0;

    private static readonly int[] Years = { 2015, 2017, 2018, 2019, 2020, 2021, 2022 };
    private static readonly string[] Volumes = { "I", "II", "III" };
    private static readonly string[] Semesters = { "1", "2" };

    public static IEnumerable<Row> Generate(Random rng)
    {
        var spamCount = (int)Math.Round(TotalRows * SpamRate);
        var hardCount = (int)Math.Round((TotalRows - spamCount) * HardExamplesRate);
        var normalCount = TotalRows - spamCount - hardCount;

        var kinds = new List<int>(TotalRows);
        kinds.AddRange(Enumerable.Repeat(0, normalCount));
        kinds.AddRange(Enumerable.Repeat(1, hardCount));
        kinds.AddRange(Enumerable.Repeat(2, spamCount));
        Shuffle(kinds, rng);

        foreach (var kind in kinds)
        {
            yield return kind switch
            {
                0 => GenerateNormal(rng),
                1 => GenerateHard(rng),
                _ => GenerateSpam(rng),
            };
        }
    }

    private static Row GenerateNormal(Random rng)
    {
        var category = PickCategory(rng);
        var items = ItemsFor(category);
        var item = items[rng.Next(items.Length)];
        var title = FillPlaceholders(item.Titles[rng.Next(item.Titles.Length)], rng);
        title = TrimTitle(title);
        var condition = PickCondition(rng);
        var description = BuildDescription(item, rng);
        var price = ComputePrice(item.BasePriceMin, item.BasePriceMax, condition, rng);
        return new Row(title, description, category, condition, price, IsSpam: false);
    }

    private static Row GenerateHard(Random rng)
    {
        var example = Catalog.HardExamples[rng.Next(Catalog.HardExamples.Length)];
        var title = TrimTitle(example.Title);
        var condition = PickCondition(rng);
        var description = rng.NextDouble() < EmptyDescriptionRate
            ? string.Empty
            : TrimDescription(example.Description ?? string.Empty);
        var price = ComputePrice(example.BasePriceMin, example.BasePriceMax, condition, rng);
        return new Row(title, description, example.Category, condition, price, IsSpam: false);
    }

    private static Row GenerateSpam(Random rng)
    {
        var disguised = rng.NextDouble() < DisguisedSpamShare;
        Category category;
        string title;
        ItemTemplate? item = null;

        if (disguised)
        {
            category = PickNonOtherCategory(rng);
            var items = ItemsFor(category);
            item = items[rng.Next(items.Length)];
            title = TrimTitle(FillPlaceholders(item.Titles[rng.Next(item.Titles.Length)], rng));
        }
        else
        {
            category = Category.Other;
            title = TrimTitle(Spam.PickTitle(rng));
        }

        var condition = PickCondition(rng);
        var description = TrimDescription(Spam.PickDescription(rng));

        int price;
        var trick = rng.Next(3);
        if (trick == 0)
        {
            price = 1;
        }
        else if (trick == 1)
        {
            price = rng.Next(50000, 100000);
        }
        else if (item is not null)
        {
            price = ComputePrice(item.BasePriceMin, item.BasePriceMax, condition, rng);
        }
        else
        {
            price = rng.Next(5, 50);
        }

        return new Row(title, description, category, condition, price, IsSpam: true);
    }

    private static Category PickCategory(Random rng)
    {
        var roll = rng.NextDouble();
        return roll switch
        {
            < 0.22 => Category.Textbook,
            < 0.42 => Category.Notes,
            < 0.65 => Category.Electronics,
            < 0.83 => Category.LabEquipment,
            _ => Category.Other,
        };
    }

    private static Category PickNonOtherCategory(Random rng)
    {
        var options = new[] { Category.Textbook, Category.Notes, Category.Electronics, Category.LabEquipment };
        return options[rng.Next(options.Length)];
    }

    private static Condition PickCondition(Random rng)
    {
        var roll = rng.NextDouble();
        return roll switch
        {
            < 0.25 => Condition.New,
            < 0.75 => Condition.Good,
            _ => Condition.Worn,
        };
    }

    private static ItemTemplate[] ItemsFor(Category category) => category switch
    {
        Category.Textbook => Catalog.Textbooks,
        Category.Notes => Catalog.Notes,
        Category.Electronics => Catalog.Electronics,
        Category.LabEquipment => Catalog.LabEquipment,
        Category.Other => Catalog.Others,
        _ => throw new InvalidOperationException($"Unknown category {category}"),
    };

    private static string BuildDescription(ItemTemplate item, Random rng)
    {
        if (rng.NextDouble() < EmptyDescriptionRate)
        {
            return string.Empty;
        }

        var sentences = 1 + rng.Next(3);
        var picked = new HashSet<int>();
        var parts = new List<string>(sentences);
        while (parts.Count < sentences && picked.Count < item.DescriptionLines.Length)
        {
            var index = rng.Next(item.DescriptionLines.Length);
            if (picked.Add(index))
            {
                parts.Add(item.DescriptionLines[index]);
            }
        }
        return TrimDescription(string.Join(" ", parts));
    }

    private static int ComputePrice(int basePriceMin, int basePriceMax, Condition condition, Random rng)
    {
        var baseValue = basePriceMin + rng.NextDouble() * (basePriceMax - basePriceMin);
        var multiplier = condition switch
        {
            Condition.New => 1.0,
            Condition.Good => 0.7,
            Condition.Worn => 0.45,
            _ => 1.0,
        };
        var noise = 0.85 + rng.NextDouble() * 0.30;
        var price = (int)Math.Round(baseValue * multiplier * noise, MidpointRounding.AwayFromZero);
        return Math.Max(1, price);
    }

    private static string FillPlaceholders(string template, Random rng)
    {
        return template
            .Replace("{VOL}", Volumes[rng.Next(Volumes.Length)])
            .Replace("{ED}", Years[rng.Next(Years.Length)].ToString(CultureInfo.InvariantCulture))
            .Replace("{SEM}", Semesters[rng.Next(Semesters.Length)]);
    }

    private static string TrimTitle(string title)
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

    private static string TrimDescription(string description)
    {
        if (description.Length > MaxDescriptionLength)
        {
            description = description[..MaxDescriptionLength];
        }
        return description;
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
