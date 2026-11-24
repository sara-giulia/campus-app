using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CampusMarket.ML.DatasetGenerator;

// Dataset v2 for CampusMarket. Fixed seed on purpose: the dataset must be reproducible across
// developers and across CI runs; the Random is not used for security.
const int Seed = 2026; // NOSONAR: deterministic dataset, not security

var catalogDir = Catalog.ResolveDefaultCatalogDirectory();
var outputPath = Path.Combine(Path.GetDirectoryName(catalogDir)!, "listings.csv");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

Console.WriteLine($"Loading catalog from {catalogDir}");
var items = Catalog.LoadAll(catalogDir);
Console.WriteLine($"Loaded {items.Count} item types across {items.Select(i => i.Category).Distinct().Count()} categories.");

var rng = new Random(Seed); // NOSONAR: fixed seed on purpose, the dataset must be reproducible
var spamRng = new Random(Seed + 1); // NOSONAR: fixed seed on purpose, only picks the pattern and text of spam rows
var generator = new Generator(rng, spamRng, items);
var rows = generator.Generate().ToList();

var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
await using (var writer = new StreamWriter(outputPath, append: false, utf8NoBom))
{
    writer.NewLine = "\n";
    await writer.WriteLineAsync("Title,Description,Category,Condition,Price,IsSpam,TemplateId");
    foreach (var row in rows)
    {
        await writer.WriteAsync(Quote(row.Title));
        await writer.WriteAsync(',');
        await writer.WriteAsync(Quote(row.Description));
        await writer.WriteAsync(',');
        await writer.WriteAsync(row.Category.ToString());
        await writer.WriteAsync(',');
        await writer.WriteAsync(row.Condition.ToString());
        await writer.WriteAsync(',');
        await writer.WriteAsync(row.Price.ToString(CultureInfo.InvariantCulture));
        await writer.WriteAsync(',');
        await writer.WriteAsync(row.IsSpam ? "true" : "false");
        await writer.WriteAsync(',');
        await writer.WriteAsync(Quote(row.TemplateId));
        await writer.WriteAsync('\n');
    }
}

var bytes = await File.ReadAllBytesAsync(outputPath);
var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
Console.WriteLine($"Wrote {rows.Count} rows to {outputPath}");
Console.WriteLine($"File size: {bytes.Length} bytes");
Console.WriteLine($"SHA256: {hash}");

PrintStats(generator, rows);

static string Quote(string value)
{
    var escaped = value.Replace("\"", "\"\"");
    return "\"" + escaped + "\"";
}

static void PrintStats(Generator generator, List<Row> rows)
{
    Console.WriteLine();
    Console.WriteLine("Item types per Category:");
    foreach (var kv in generator.ItemTypeCountPerCategory().OrderBy(kv => kv.Key))
    {
        Console.WriteLine($"  {kv.Key,-12} {kv.Value}");
    }

    Console.WriteLine();
    Console.WriteLine("Rows per Category:");
    foreach (var group in rows.GroupBy(r => r.Category).OrderBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key,-12} {group.Count()}");
    }

    Console.WriteLine();
    Console.WriteLine("Rows per Condition:");
    foreach (var group in rows.GroupBy(r => r.Condition).OrderBy(g => g.Key))
    {
        Console.WriteLine($"  {group.Key,-6} {group.Count()}");
    }

    var spam = rows.Count(r => r.IsSpam);
    var pct = 100.0 * spam / rows.Count;
    Console.WriteLine();
    Console.WriteLine($"Spam rows: {spam} ({pct.ToString("F2", CultureInfo.InvariantCulture)}%)");

    var distinctTemplates = rows.Select(r => r.TemplateId).Distinct().Count();
    Console.WriteLine($"Distinct TemplateIds in CSV: {distinctTemplates}");

    Console.WriteLine();
    Console.WriteLine("Price per Category (min / avg / max, lei):");
    foreach (var group in rows.GroupBy(r => r.Category).OrderBy(g => g.Key))
    {
        var min = group.Min(r => r.Price);
        var avg = group.Average(r => r.Price);
        var max = group.Max(r => r.Price);
        Console.WriteLine($"  {group.Key,-12} {min,6} / {avg.ToString("F0", CultureInfo.InvariantCulture),6} / {max,6}");
    }

    Console.WriteLine();
    Console.WriteLine("10 random rows (deterministic Random(123)):");
    var sampleRng = new Random(123); // NOSONAR: fixed seed on purpose, the sample printout must be reproducible
    var picks = Enumerable.Range(0, 10).Select(_ => rows[sampleRng.Next(rows.Count)]);
    foreach (var row in picks)
    {
        var description = row.Description.Length > 70 ? row.Description[..70] + "..." : row.Description;
        Console.WriteLine($"  [{row.Category}/{row.Condition}/{row.Price} lei/spam={row.IsSpam}/{row.TemplateId}] {row.Title}");
        if (!string.IsNullOrEmpty(description))
        {
            Console.WriteLine($"      {description}");
        }
    }
}
