using System.Globalization;
using System.Text;

namespace CampusMarket.Api.Common.Search;

public static class SearchText
{
    // Lower case and without diacritics, so "Căști" finds "Casti" and "ȘTIINȚIFIC" finds "stiintific".
    // Both forms of ș and ț (comma and cedilla) lose their mark.
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
