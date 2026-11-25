namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed record WmcManualProductUnitValidationResult(
    bool IsValid,
    IReadOnlyList<string> Reasons,
    string? PrimaryUnitCode,
    string? MasterUnitCode,
    WmcProductUnitRecord? PrimaryUnit,
    WmcProductUnitRecord? MasterUnit,
    string? ProposedEan,
    string? ProposedEan13,
    string? ProposedDun14,
    decimal? ProposedPackageQuantity,
    decimal? ProposedConversionQuantity)
{
    public string? BlockReason => Reasons.Count == 0 ? null : string.Join("; ", Reasons);
}

public static class WmcManualProductUnitValidation
{
    private static readonly int[] GtinLengths = [8, 12, 13, 14];

    public static WmcManualProductUnitValidationResult Validate(
        WmcProductRecord source,
        IReadOnlyCollection<WmcProductUnitRecord> units,
        bool snapshotComplete)
    {
        var reasons = new List<string>();
        var byCode = new Dictionary<string, WmcProductUnitRecord>(StringComparer.Ordinal);

        if (!snapshotComplete)
            reasons.Add("snapshot de PRODUTOS_UNCONV incompleto");
        if (units.Count == 0)
            reasons.Add("snapshot de PRODUTOS_UNCONV vazio");

        foreach (var unit in units)
        {
            var code = Normalize(unit.UnitCode);
            if (code.Length == 0)
            {
                reasons.Add("código de unidade vazio");
                continue;
            }

            if (!byCode.TryAdd(code, unit))
            {
                reasons.Add($"unidade WMC duplicada: {code}");
                continue;
            }

            if (unit.Factor is not > 0)
                reasons.Add($"fator da unidade {code} ausente ou inválido");

            if (Clean(unit.Barcode) is { } barcode && !IsValidBarcode(barcode))
                reasons.Add($"código de barras da unidade {code} ausente ou inválido");
        }

        var primaryCode = Normalize(source.Un);
        WmcProductUnitRecord? primary = null;
        if (primaryCode.Length == 0)
            reasons.Add("unidade principal PRODUTOS.UN ausente");
        else if (!byCode.TryGetValue(primaryCode, out primary))
            reasons.Add($"unidade principal PRODUTOS.UN não encontrada no snapshot: {primaryCode}");

        var masters = units
            .Where(unit => unit.IsMaster && Normalize(unit.UnitCode).Length > 0)
            .ToArray();
        if (masters.Length > 1)
            reasons.Add("mais de uma unidade master confirmada pelo WMC");
        var master = masters.Length == 1 ? masters[0] : null;
        var masterCode = master is null ? null : Normalize(master.UnitCode);

        var alternateCode = Normalize(source.UnAltern);
        if (alternateCode.Length > 0 && !byCode.ContainsKey(alternateCode))
            reasons.Add($"UN_ALTERN não encontrada no snapshot: {alternateCode}");

        if (Clean(source.Ean) is { } sourceEan && !IsValidBarcode(sourceEan))
            reasons.Add("CODBARRASFAB/EAN principal informado é inválido");
        if (source.QtdeEmbalagem is decimal packageQuantity && packageQuantity <= 0)
            reasons.Add("QTDE_EMB informada é zero ou negativa");
        if (source.QtdeConversao is decimal conversionQuantity && conversionQuantity <= 0)
            reasons.Add("QTDE_CONV informada é zero ou negativa");

        var primaryBarcode = primary is null
            ? Clean(source.Ean)
            : Clean(primary.Barcode) ?? Clean(source.Ean);
        var proposedEan = primaryBarcode is not null && IsValidBarcode(primaryBarcode) ? primaryBarcode : null;
        var proposedEan13 = proposedEan?.Length == 13 ? proposedEan : null;
        var masterBarcode = Clean(master?.Barcode);
        var proposedDun14 = masterBarcode?.Length == 14 && IsValidBarcode(masterBarcode) ? masterBarcode : null;

        return new(
            reasons.Count == 0,
            reasons,
            primaryCode.Length == 0 ? null : primaryCode,
            masterCode,
            primary,
            master,
            proposedEan,
            proposedEan13,
            proposedDun14,
            source.QtdeEmbalagem,
            source.QtdeConversao);
    }

    public static bool IsValidBarcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var digits = value.Trim();
        if (!GtinLengths.Contains(digits.Length) || digits.Any(character => character is < '0' or > '9')) return false;

        var sum = 0;
        for (var index = digits.Length - 2; index >= 0; index--)
        {
            var positionFromRight = digits.Length - 1 - index;
            sum += (digits[index] - '0') * (positionFromRight % 2 == 1 ? 3 : 1);
        }

        return (10 - (sum % 10)) % 10 == digits[^1] - '0';
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string? value) => (Clean(value) ?? string.Empty).ToUpperInvariant();
}
