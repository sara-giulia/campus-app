using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampusMarket.ML.DatasetGenerator;

public sealed record ItemTemplate(
    string Id,
    Category Category,
    string[] Titles,
    string[] DescriptionLines,
    int BasePriceMin,
    int BasePriceMax);

// Loads the per-category JSON files committed in ml/data/catalog/ so the dataset generator
// is driven by data instead of hard-coded C# arrays. The JSON layout is described in
// ml/data/README.md; one file per category, named textbook.json / notes.json / electronics.json
// / labequipment.json / other.json.
public static class Catalog
{
    private const string CatalogFolderName = "catalog";

    private static readonly string[] CategoryFiles =
    {
        "textbook.json",
        "notes.json",
        "electronics.json",
        "labequipment.json",
        "other.json",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<ItemTemplate> LoadAll(string catalogDirectory)
    {
        var items = new List<ItemTemplate>();
        foreach (var fileName in CategoryFiles)
        {
            var path = Path.Combine(catalogDirectory, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Catalog file missing: {path}. Expected 5 files in {catalogDirectory}.");
            }

            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<CatalogFile>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Could not parse {path}");
            foreach (var item in file.Items)
            {
                items.Add(new ItemTemplate(
                    Id: item.Id,
                    Category: file.Category,
                    Titles: item.Titles,
                    DescriptionLines: item.Descriptions,
                    BasePriceMin: item.PriceMin,
                    BasePriceMax: item.PriceMax));
            }
        }
        return items;
    }

    public static string ResolveDefaultCatalogDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null && dir.Name != "CampusMarket.ML.DatasetGenerator")
        {
            dir = dir.Parent;
        }
        if (dir?.Parent is null)
        {
            throw new InvalidOperationException("Could not locate the ml/ directory from " + baseDir);
        }
        return Path.Combine(dir.Parent.FullName, "data", CatalogFolderName);
    }

    private sealed class CatalogFile
    {
        public Category Category { get; set; }

        public CatalogItem[] Items { get; set; } = Array.Empty<CatalogItem>();
    }

    private sealed class CatalogItem
    {
        public string Id { get; set; } = string.Empty;

        public string[] Titles { get; set; } = Array.Empty<string>();

        public string[] Descriptions { get; set; } = Array.Empty<string>();

        public int PriceMin { get; set; }

        public int PriceMax { get; set; }
    }
}
