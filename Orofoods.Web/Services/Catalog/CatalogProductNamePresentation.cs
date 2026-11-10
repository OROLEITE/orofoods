using System.Globalization;

namespace Orofoods.Web.Services.Catalog;

public static class CatalogProductNamePresentation
{
    private const string BrandPrefix = "BIMBO";

    public static string DisplayName(string? name)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith(BrandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return name ?? string.Empty;
        }

        if (name.Length > BrandPrefix.Length && IsWordContinuation(name, BrandPrefix.Length))
        {
            return name;
        }

        var contentStart = BrandPrefix.Length;
        while (contentStart < name.Length && char.IsWhiteSpace(name[contentStart]))
        {
            contentStart++;
        }

        return name[contentStart..];
    }

    private static bool IsWordContinuation(string value, int index)
    {
        var character = value[index];
        if (char.IsLetterOrDigit(character) || character == '_')
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(value, index) is
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark;
    }
}
