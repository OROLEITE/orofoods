namespace Orofoods.Web.Integrations.Erp.Wmc;

internal enum WmcProductSituation
{
    Active,
    Inactive,
    Unknown
}

internal static class WmcProductRules
{
    public static string NormalizeCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static WmcProductSituation ClassifySituation(string? situation) => situation?.Trim().ToUpperInvariant() switch
    {
        "A" => WmcProductSituation.Active,
        "I" => WmcProductSituation.Inactive,
        _ => WmcProductSituation.Unknown
    };

    public static List<string> FindDuplicateCodes(IEnumerable<WmcProductRecord> rows) => rows
        .Where(row => !string.IsNullOrWhiteSpace(row.CodProduto))
        .GroupBy(row => NormalizeCode(row.CodProduto), StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();

    public static bool TryNormalizeStock(decimal? value, out int quantity, out string reason)
    {
        quantity = 0;
        reason = "valor ausente";
        if (!value.HasValue)
        {
            return false;
        }

        if (value.Value < 0)
        {
            reason = "valor negativo; disponibilidade definida como zero";
            return true;
        }

        if (decimal.Truncate(value.Value) != value.Value || value.Value > int.MaxValue)
        {
            reason = "valor fracionário ou fora do intervalo suportado";
            return false;
        }

        quantity = (int)value.Value;
        reason = "";
        return true;
    }
}
