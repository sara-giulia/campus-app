using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CampusMarket.Api.Infrastructure.ML;

// The four explicit text features of the price model (price v2, "Text" feature set).
//
// One source of truth: this file lives in the API and is compiled into the training project as a
// linked file (see CampusMarket.ML.Training.csproj), so training and serving extract the features
// with the very same code and cannot drift. Keep it dependency free (no ML.NET, no API types).
//
// Everything works on title + description, lowercased and stripped of diacritics, so "Sigilat",
// "SIGILAT", "ȘIGILAT" or "Nou"/"nou" all read the same, and both the comma-below (ș, ț) and the
// cedilla (ş, ţ) Romanian forms fold to plain s / t.
public static class PriceTextFeatures
{
    public const string NoBrand = "none";

    public sealed record Values(string Brand, float StorageGb, float CpuTier, float IsNewOrSealed);

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    // A number followed by GB or TB, e.g. "16GB", "512 GB", "1TB", "1,5 TB".
    private static readonly Regex StorageRegex = new(
        @"(?<![a-z0-9.,])(\d+(?:[.,]\d{1,2})?)\s?(gb|tb)(?![a-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Intel Core i3/i5/i7/i9 ("i5", "i7-8550U") and AMD Ryzen 3/5/7/9 ("Ryzen 5 3600").
    private static readonly Regex CpuRegex = new(
        @"(?<![a-z0-9])(?:i([3579])(?![a-z0-9])|ryzen\s?([3579])(?![0-9]))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Whole words only: "nou" must not match inside "noua", "renovat", "nouă" and so on.
    private static readonly Regex NewOrSealedRegex = new(
        @"(?<![a-z0-9])(?:sigilat|nou|new|sealed)(?![a-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex WordRegex = new(
        "[a-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    // Fixed brand list, built from the brands that appear in the titles of ml/data/catalog/*.json.
    // The key is what is looked for (one word, or two words separated by one space); the value is
    // the brand the model sees. Product lines map to their maker (thinkpad -> lenovo).
    private static readonly Dictionary<string, string> BrandByTerm = BuildBrandTerms();

    public static string[] Brands { get; } = BrandByTerm.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static Values Extract(string? title, string? description)
    {
        var text = Normalize($"{title} {description}");
        return new Values(
            Brand: FindBrand(text),
            StorageGb: FindStorageGb(text),
            CpuTier: FindCpuTier(text),
            IsNewOrSealed: NewOrSealedRegex.IsMatch(text) ? 1f : 0f);
    }

    // Lowercase + remove diacritics (FormD splits ă, â, î, ș, ş, ț, ţ into letter + combining mark).
    internal static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // The first brand mentioned wins (the title comes first); two-word brands are tried first.
    private static string FindBrand(string text)
    {
        var words = WordRegex.Matches(text).Select(m => m.Value).ToArray();
        for (var i = 0; i < words.Length; i++)
        {
            if (i + 1 < words.Length && BrandByTerm.TryGetValue($"{words[i]} {words[i + 1]}", out var pair))
            {
                return pair;
            }

            if (BrandByTerm.TryGetValue(words[i], out var single))
            {
                return single;
            }
        }

        return NoBrand;
    }

    private static float FindStorageGb(string text)
    {
        var largest = 0.0;
        foreach (Match m in StorageRegex.Matches(text))
        {
            var number = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var gb = m.Groups[2].Value == "tb" ? number * 1024 : number;
            largest = Math.Max(largest, gb);
        }

        return (float)largest;
    }

    private static float FindCpuTier(string text)
    {
        var best = 0;
        foreach (Match m in CpuRegex.Matches(text))
        {
            var digit = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            best = Math.Max(best, int.Parse(digit, CultureInfo.InvariantCulture));
        }

        return best;
    }

    private static Dictionary<string, string> BuildBrandTerms()
    {
        // brand -> extra terms that identify it (product lines, two-word spellings).
        var brands = new (string Brand, string[] Aliases)[]
        {
            ("lenovo", ["thinkpad", "ideapad", "legion"]),
            ("asus", ["vivobook", "zenbook", "rog", "tuf", "proart", "zenwifi"]),
            ("dell", ["latitude", "xps", "inspiron", "vostro"]),
            ("hp", ["pavilion", "probook", "omen", "elitebook", "zbook", "envy", "laserjet"]),
            ("acer", ["aspire", "swift", "travelmate", "predator", "nitro"]),
            ("msi", []),
            ("gigabyte", ["aorus"]),
            ("apple", ["macbook", "iphone", "ipad", "airpods", "magsafe"]),
            ("samsung", ["galaxy", "odyssey"]),
            ("xiaomi", ["redmi", "poco"]),
            ("huawei", []),
            ("oneplus", []),
            ("sony", ["playstation", "linkbuds"]),
            ("microsoft", ["xbox", "surface", "lifecam"]),
            ("lg", ["ultragear"]),
            ("philips", []),
            ("panasonic", []),
            ("garmin", []),
            ("bose", []),
            ("sennheiser", []),
            ("jbl", []),
            ("marshall", []),
            ("akg", []),
            ("shure", []),
            ("beyerdynamic", []),
            ("audio technica", []),
            ("logitech", []),
            ("razer", []),
            ("corsair", []),
            ("steelseries", []),
            ("hyperx", []),
            ("keychron", []),
            ("ducky", []),
            ("glorious", []),
            ("kingston", []),
            ("sandisk", []),
            ("crucial", []),
            ("seagate", []),
            ("western digital", []),
            ("toshiba", []),
            ("adata", []),
            ("lexar", []),
            ("transcend", []),
            ("sabrent", []),
            ("teamgroup", []),
            ("patriot", []),
            ("intel", []),
            ("amd", []),
            ("nvidia", []),
            ("benq", []),
            ("aoc", []),
            ("viewsonic", []),
            ("tp link", ["archer"]),
            ("netgear", ["nighthawk"]),
            ("canon", []),
            ("nikon", []),
            ("fujifilm", []),
            ("gopro", []),
            ("dji", []),
            ("anker", ["soundcore", "nebula"]),
            ("casio", ["classwiz"]),
            ("texas instruments", []),
            ("brother", []),
            ("epson", []),
            ("wacom", []),
            ("amazon", ["kindle"]),
            ("arduino", []),
            ("raspberry", []),
            ("fluke", []),
            ("siglent", []),
            ("rigol", []),
            ("hantek", []),
            ("owon", []),
            ("hakko", []),
            ("weller", []),
            ("omron", []),
            ("beurer", []),
            ("microlife", []),
            ("littmann", []),
            ("olympus", []),
            ("celestron", []),
            ("bresser", []),
            ("levenhuk", []),
            ("lego", []),
            ("ikea", []),
            ("jysk", []),
            ("bellona", []),
            ("dormeo", []),
            ("tefal", []),
            ("rowenta", []),
            ("bosch", []),
            ("dyson", []),
            ("kenwood", []),
            ("nespresso", []),
            ("krups", []),
            ("cecotec", []),
            ("smeg", []),
            ("beko", []),
            ("whirlpool", []),
            ("hoover", []),
            ("honeywell", []),
            ("babyliss", []),
            ("remington", []),
            ("nike", []),
            ("adidas", []),
            ("puma", []),
            ("reebok", []),
            ("new balance", []),
            ("under armour", []),
            ("columbia", []),
            ("salomon", []),
            ("merrell", []),
            ("timberland", []),
            ("ecco", []),
            ("jack wolfskin", []),
            ("north face", []),
            ("patagonia", []),
            ("uniqlo", []),
            ("herschel", []),
            ("eastpak", []),
            ("fjallraven", []),
            ("samsonite", []),
            ("decathlon", ["btwin", "quechua", "rockrider"]),
            ("giant", []),
            ("cube", []),
            ("trek", []),
            ("specialized", []),
            ("cannondale", []),
            ("merida", []),
            ("shimano", []),
            ("dahon", []),
            ("brompton", []),
            ("fiido", []),
        };

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (brand, aliases) in brands)
        {
            map[brand] = brand;
            foreach (var alias in aliases)
            {
                map[alias] = brand;
            }
        }

        return map;
    }
}
