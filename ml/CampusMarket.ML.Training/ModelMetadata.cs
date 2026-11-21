using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CampusMarket.ML.Training;

// Writes <model-file>.json beside each saved .zip. The JSON is small, sorted
// alphabetically by key (ordinal) and every metric uses a fixed number of
// decimal places, so a `git diff` between two runs only highlights real
// differences and not float formatting.
public static class ModelMetadata
{
    private const int MetricDecimals = 6;
    private const string DateFormat = "yyyy-MM-ddTHH:mm:ssZ";
    private const string SplitDescription = "70/15/15 grouped by TemplateId";

    public static void Save(
        string modelFilePath,
        string modelName,
        string algorithm,
        IReadOnlyDictionary<string, double> testMetrics,
        string csvPath,
        int trainRowCount,
        int validationRowCount,
        int testRowCount)
    {
        var json = Build(
            modelName,
            Path.GetFileName(modelFilePath),
            algorithm,
            testMetrics,
            csvPath,
            trainRowCount,
            validationRowCount,
            testRowCount,
            DateTimeOffset.UtcNow);

        var jsonPath = Path.ChangeExtension(modelFilePath, ".json");
        File.WriteAllText(jsonPath, json);
    }

    public static string Build(
        string modelName,
        string modelFileName,
        string algorithm,
        IReadOnlyDictionary<string, double> testMetrics,
        string csvPath,
        int trainRowCount,
        int validationRowCount,
        int testRowCount,
        DateTimeOffset trainedAt)
    {
        var sha = ComputeSha256(csvPath);
        var trainedAtText = trainedAt.ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture);
        var gitCommit = Environment.GetEnvironmentVariable("GITHUB_SHA");

        var sb = new StringBuilder();
        sb.Append('{').Append('\n');

        AppendString(sb, 1, "algorithm", algorithm, comma: true);

        sb.Append(Indent(1)).Append("\"dataset\": {").Append('\n');
        AppendInt(sb, 2, "rowCountTest", testRowCount, comma: true);
        AppendInt(sb, 2, "rowCountTrain", trainRowCount, comma: true);
        AppendInt(sb, 2, "rowCountValidation", validationRowCount, comma: true);
        AppendString(sb, 2, "sha256", sha, comma: false);
        sb.Append(Indent(1)).Append("},").Append('\n');

        AppendStringOrNull(sb, 1, "gitCommit", gitCommit, comma: true);
        AppendString(sb, 1, "modelFileName", modelFileName, comma: true);
        AppendString(sb, 1, "modelName", modelName, comma: true);
        AppendInt(sb, 1, "seed", DatasetSplit.Seed, comma: true);
        AppendString(sb, 1, "split", SplitDescription, comma: true);

        sb.Append(Indent(1)).Append("\"testMetrics\": {").Append('\n');
        var sortedKeys = testMetrics.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        for (var i = 0; i < sortedKeys.Count; i++)
        {
            var key = sortedKeys[i];
            AppendDouble(sb, 2, key, testMetrics[key], comma: i < sortedKeys.Count - 1);
        }
        sb.Append(Indent(1)).Append("},").Append('\n');

        AppendString(sb, 1, "trainedAt", trainedAtText, comma: false);

        sb.Append('}').Append('\n');
        return sb.ToString();
    }

    private static string Indent(int level) => new(' ', level * 2);

    private static void AppendString(StringBuilder sb, int indent, string key, string value, bool comma)
    {
        sb.Append(Indent(indent)).Append('"').Append(key).Append("\": \"").Append(Escape(value)).Append('"');
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendStringOrNull(StringBuilder sb, int indent, string key, string? value, bool comma)
    {
        sb.Append(Indent(indent)).Append('"').Append(key).Append("\": ");
        if (value is null)
        {
            sb.Append("null");
        }
        else
        {
            sb.Append('"').Append(Escape(value)).Append('"');
        }
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendInt(StringBuilder sb, int indent, string key, int value, bool comma)
    {
        sb.Append(Indent(indent)).Append('"').Append(key).Append("\": ")
            .Append(value.ToString(CultureInfo.InvariantCulture));
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static void AppendDouble(StringBuilder sb, int indent, string key, double value, bool comma)
    {
        var rounded = Math.Round(value, MetricDecimals, MidpointRounding.AwayFromZero);
        var text = rounded.ToString("0." + new string('0', MetricDecimals), CultureInfo.InvariantCulture);
        sb.Append(Indent(indent)).Append('"').Append(key).Append("\": ").Append(text);
        if (comma) sb.Append(',');
        sb.Append('\n');
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
