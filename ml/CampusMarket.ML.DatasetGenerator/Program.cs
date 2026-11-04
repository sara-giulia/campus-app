using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CampusMarket.ML.DatasetGenerator;

var outputPath = ResolveOutputPath();
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var rng = new Random(42);
var rows = Generator.Generate(rng).ToList();

var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
using (var writer = new StreamWriter(outputPath, append: false, utf8NoBom))
{
    writer.NewLine = "\n";
    writer.WriteLine("Title,Description,Category,Condition,Price,IsSpam");
    foreach (var row in rows)
    {
        writer.Write(Quote(row.Title));
        writer.Write(',');
        writer.Write(Quote(row.Description));
        writer.Write(',');
        writer.Write(row.Category);
        writer.Write(',');
        writer.Write(row.Condition);
        writer.Write(',');
        writer.Write(row.Price.ToString(CultureInfo.InvariantCulture));
        writer.Write(',');
        writer.Write(row.IsSpam ? "true" : "false");
        writer.Write('\n');
    }
}

var bytes = File.ReadAllBytes(outputPath);
var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
Console.WriteLine($"Wrote {rows.Count} rows to {outputPath}");
Console.WriteLine($"File size: {bytes.Length} bytes");
Console.WriteLine($"SHA256: {hash}");

PrintStats(rows);

static string Quote(string value)
{
    var escaped = value.Replace("\"", "\"\"");
    return "\"" + escaped + "\"";
}

static string ResolveOutputPath()
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
    return Path.Combine(dir.Parent.FullName, "data", "listings.csv");
}

static void PrintStats(List<Row> rows)
{
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
    var sampleRng = new Random(123);
    var picks = Enumerable.Range(0, 10).Select(_ => rows[sampleRng.Next(rows.Count)]);
    foreach (var row in picks)
    {
        var description = row.Description.Length > 70 ? row.Description[..70] + "..." : row.Description;
        Console.WriteLine($"  [{row.Category}/{row.Condition}/{row.Price} lei/spam={row.IsSpam}] {row.Title}");
        if (!string.IsNullOrEmpty(description))
        {
            Console.WriteLine($"      {description}");
        }
    }
}
