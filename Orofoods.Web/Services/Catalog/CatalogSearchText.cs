using System.Globalization;
using System.Text;

namespace Orofoods.Web.Services.Catalog;

internal static class CatalogSearchText
{
    public static string Normalize(string value) => string.Concat(
            value.Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) is not (
                    UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)))
        .ToUpperInvariant();
}
