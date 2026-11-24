using System.Globalization;
using System.Security;
using System.Text;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Infrastructure.Images;

// A simple picture drawn by the app: the colour and icon of the category, the title in the middle and the
// category name under it. Only text the seller wrote, nothing invented, and every text is XML-escaped, so
// a title cannot add markup to the SVG. Works everywhere (no model, no GPU), so it is the default.
public sealed class SvgImageGenerator : IImageGenerator
{
    private const int Width = 800;
    private const int Height = 600;
    private const int MaxLineLength = 24;
    private const int MaxLines = 3;

    // Background, accent (icon and category name) and the icon's drawing, per category.
    private static readonly Dictionary<Category, (string Background, string Accent, string Icon)> Styles = new()
    {
        [Category.Textbook] = ("#fde7e1", "#c9361f",
            "<rect x='-50' y='-60' width='100' height='120' rx='8'/><line x1='-30' y1='-60' x2='-30' y2='60'/>"),
        [Category.Notes] = ("#fff4d6", "#b7791f",
            "<rect x='-45' y='-60' width='90' height='120' rx='6'/><line x1='-25' y1='-25' x2='25' y2='-25'/>"
            + "<line x1='-25' y1='0' x2='25' y2='0'/><line x1='-25' y1='25' x2='10' y2='25'/>"),
        [Category.Electronics] = ("#e3ecfb", "#2f5fb3",
            "<rect x='-60' y='-50' width='120' height='75' rx='6'/><path d='M-80 40 H80 L68 55 H-68 Z'/>"),
        [Category.LabEquipment] = ("#e2f4ec", "#1f7a55",
            "<path d='M-18 -60 H18 M-12 -60 V-15 L-50 50 Q-55 60 -44 60 H44 Q55 60 50 50 L12 -15 V-60'/>"),
        [Category.Other] = ("#efe9f7", "#6b46c1",
            "<path d='M-55 -15 L0 -50 L55 -15 V45 H-55 Z'/><line x1='-55' y1='-15' x2='55' y2='-15'/>"),
    };

    public Task<GeneratedImage> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default)
    {
        var (background, accent, icon) = Styles[prompt.Category];
        var lines = WrapTitle(prompt.Title);
        var firstLineY = 360 - (lines.Count - 1) * 26;

        var svg = new StringBuilder()
            .Append(CultureInfo.InvariantCulture,
                $"<svg xmlns='http://www.w3.org/2000/svg' width='{Width}' height='{Height}' viewBox='0 0 {Width} {Height}'>")
            .Append(CultureInfo.InvariantCulture, $"<rect width='{Width}' height='{Height}' fill='{background}'/>")
            .Append(CultureInfo.InvariantCulture,
                $"<g transform='translate(400 190)' fill='none' stroke='{accent}' stroke-width='8' stroke-linecap='round' stroke-linejoin='round'>{icon}</g>")
            .Append("<g font-family='Figtree, Segoe UI, Arial, sans-serif' text-anchor='middle'>");

        for (var i = 0; i < lines.Count; i++)
            svg.Append(CultureInfo.InvariantCulture,
                $"<text x='400' y='{firstLineY + i * 52}' font-size='44' font-weight='700' fill='#1f1f1f'>{Escape(lines[i])}</text>");

        svg.Append(CultureInfo.InvariantCulture,
                $"<text x='400' y='{firstLineY + lines.Count * 52 + 20}' font-size='28' fill='{accent}'>{Escape(prompt.CategoryName)}</text>")
            .Append("</g></svg>");

        var image = new GeneratedImage(Encoding.UTF8.GetBytes(svg.ToString()), PhotoFormat.Svg);
        return Task.FromResult(image);
    }

    // SVG text does not wrap by itself: whole words on lines of at most MaxLineLength characters, at most
    // MaxLines lines, a longer word cut, and "…" when the title does not fit.
    private static List<string> WrapTitle(string title)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = word.Length > MaxLineLength ? word[..(MaxLineLength - 1)] + "…" : word;
            if (current.Length > 0 && current.Length + 1 + piece.Length > MaxLineLength)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            current.Append(current.Length > 0 ? " " : string.Empty).Append(piece);
        }

        lines.Add(current.ToString());

        if (lines.Count <= MaxLines)
            return lines;

        // The last kept line ends in "…": whole words dropped until it fits, so no word is cut in two.
        var kept = lines.Take(MaxLines).ToList();
        var last = kept[^1].TrimEnd('…');
        while (last.Length + 1 > MaxLineLength && last.Contains(' '))
            last = last[..last.LastIndexOf(' ')];
        kept[^1] = last[..Math.Min(last.Length, MaxLineLength - 1)] + "…";
        return kept;
    }

    private static string Escape(string text) => SecurityElement.Escape(text);
}
