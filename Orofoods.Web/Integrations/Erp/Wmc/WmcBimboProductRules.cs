using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public static class WmcBimboProductRules
{
    public const short BrandCode = 104;
    private const string Product504342Code = "504342";
    public const string CommercialStatusPending = "Pendente";
    public const string CommercialStatusInactive = "Inativo";
    public const string CommercialStatusActive = "Ativo";

    private static readonly string[] Codes =
    ["503768", "503775", "503777", "503805", "503811", "503813", "503833", "503838", "504338", "504342"];

    private static readonly IReadOnlyDictionary<string, string> EanReferences = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["503768"] = "7899679700365",
        ["503775"] = "7899679701478",
        ["503777"] = "7899679701751",
        ["503805"] = "7899679700327",
        ["503811"] = "7899679701072",
        ["503813"] = "7899679701348",
        ["503833"] = "7899679702130",
        ["503838"] = "7899679702659",
        ["504338"] = "7899679702307",
        ["504342"] = "7899679704936"
    };

    private static readonly Regex PackageCasePattern = new(
        @"(?<![A-Z0-9])(?<packages>[0-9]+)\s*PCX\s*[1-9][0-9]*\s*UN(?![A-Z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PackageCaseMarkerPattern = new(
        "PCX",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> ApprovedProductCodes { get; } = Array.AsReadOnly(Codes);

    public static string NormalizeCode(string? value) => WmcProductRules.NormalizeCode(value);

    public static bool IsApprovedEan13(string? wmcCode, string? value) =>
        EanReferences.TryGetValue(NormalizeCode(wmcCode), out var expected)
        && string.Equals(value?.Trim(), expected, StringComparison.Ordinal);

    public static int? ExtractPackagesPerCase(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var matches = PackageCasePattern.Matches(description);
        if (PackageCaseMarkerPattern.Matches(description).Count != 1
            || matches.Count != 1
            || !int.TryParse(matches[0].Groups["packages"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var packages)
            || packages <= 0)
            return null;

        return packages;
    }

    public static IReadOnlyList<WmcBimboValidationIssue> ValidateSourceRow(string code, WmcProductRecord? row)
    {
        if (row is null)
            return [new("APPROVED_PRODUCT_MISSING", "Produto não encontrado no espelho WMC.")];

        var issues = new List<WmcBimboValidationIssue>();
        if (!string.Equals(row.CodMarca?.Trim(), BrandCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
            issues.Add(new("SOURCE_BRAND_MISMATCH", "A marca do registro não corresponde à BIMBO (104)."));
        if (WmcProductRules.ClassifySituation(row.Situacao) != WmcProductSituation.Active)
            issues.Add(new("SOURCE_PRODUCT_NOT_ACTIVE", "Produto inativo ou sem situação válida no WMC."));
        if (!string.Equals(row.Un?.Trim(), "PC", StringComparison.OrdinalIgnoreCase))
            issues.Add(new("SOURCE_STOCK_UNIT_MISMATCH", "A unidade de estoque WMC não é PC."));
        if (string.IsNullOrWhiteSpace(row.Produto) || row.Produto.Trim().Length > 140)
            issues.Add(new("SOURCE_PRODUCT_NAME_INVALID", "Descrição ausente ou maior que 140 caracteres."));

        var ean13 = row.Ean?.Trim();
        if (!IsValidBarcode(ean13, 13))
            issues.Add(new("SOURCE_EAN13_INVALID", "EAN-13 ausente ou com dígito verificador inválido."));
        else if (!IsApprovedEan13(code, ean13))
            issues.Add(new("SOURCE_EAN13_MISMATCH", "EAN-13 diferente da referência homologada para este código WMC."));

        if (row.EstoqueAtual is not decimal stock || stock < 0 || decimal.Truncate(stock) != stock || stock > int.MaxValue)
            issues.Add(new("SOURCE_STOCK_INVALID", "Estoque ausente, negativo, fracionário ou fora do limite aceito."));

        return issues;
    }

    public static IReadOnlyList<WmcBimboValidationIssue> ValidateCommercialData(
        Product product,
        ProductCategory? category,
        ProductInventory? inventory)
    {
        var issues = new List<WmcBimboValidationIssue>();
        var code = NormalizeCode(product.WmcCode);
        if (!Codes.Contains(code, StringComparer.Ordinal) || !string.Equals(product.WmcBrandCode, BrandCode.ToString(), StringComparison.Ordinal))
            issues.Add(new("LOCAL_PRODUCT_SCOPE_MISMATCH", "Produto fora do lote BIMBO homologado."));
        if (string.IsNullOrWhiteSpace(product.Sku) || string.IsNullOrWhiteSpace(product.Name))
            issues.Add(new("COMMERCIAL_IDENTITY_INCOMPLETE", "SKU e nome comercial são obrigatórios."));
        if (category is null || !category.IsActive)
            issues.Add(new("COMMERCIAL_CATEGORY_INACTIVE", "Selecione uma categoria comercial ativa."));
        if (product.BasePrice <= 0)
            issues.Add(new("COMMERCIAL_PRICE_INVALID", "Informe um preço base maior que zero."));
        if (!IsValidBarcode(product.Ean13, 13) || !IsApprovedEan13(code, product.Ean13)
            || !string.Equals(product.Ean?.Trim(), product.Ean13?.Trim(), StringComparison.Ordinal))
            issues.Add(new("COMMERCIAL_EAN_INVALID", "O EAN-13 deve ser válido e corresponder ao código WMC homologado."));
        if (!IsValidBarcode(product.Dun14, 14))
            issues.Add(new("COMMERCIAL_DUN14_INVALID", "Informe um DUN-14 válido com dígito verificador correto."));
        if (!product.HasPackageSalesConfiguration)
            issues.Add(new("COMMERCIAL_PACKAGE_CONFIGURATION_INCOMPLETE", "Informe unidade PCT, unidade logística CX e fator de pacotes por caixa."));
        if (!product.IsWmcActive)
            issues.Add(new("WMC_PRODUCT_INACTIVE", "O produto está inativo no WMC."));
        if (!product.WmcStockAvailable || inventory is null || inventory.QuantityOnHand - inventory.QuantityReserved < 1)
            issues.Add(new("WMC_STOCK_UNAVAILABLE", "O produto precisa ter ao menos um pacote disponível no estoque WMC."));
        return issues;
    }

    public static IReadOnlyList<WmcBimboValidationIssue> ValidateStagingActivationData(Product product, ProductInventory? inventory)
    {
        var issues = new List<WmcBimboValidationIssue>();
        var code = NormalizeCode(product.WmcCode);
        if (!Codes.Contains(code, StringComparer.Ordinal) || !string.Equals(product.WmcBrandCode, BrandCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            issues.Add(new("LOCAL_PRODUCT_SCOPE_MISMATCH", "Produto fora do lote BIMBO homologado."));
        if (string.IsNullOrWhiteSpace(product.Sku) || string.IsNullOrWhiteSpace(product.Name))
            issues.Add(new("COMMERCIAL_IDENTITY_INCOMPLETE", "SKU e nome comercial são obrigatórios."));
        if (product.BasePrice <= 0)
            issues.Add(new("COMMERCIAL_PRICE_INVALID", "Informe um preço base maior que zero."));
        if (!IsValidBarcode(product.Ean13, 13) || !IsApprovedEan13(code, product.Ean13)
            || !string.Equals(product.Ean?.Trim(), product.Ean13?.Trim(), StringComparison.Ordinal))
            issues.Add(new("COMMERCIAL_EAN_INVALID", "O EAN-13 deve ser válido e corresponder ao código WMC homologado."));
        if (!IsAcceptedStagingUnit(code, product.Unit) || !product.HasPackageSalesConfiguration)
            issues.Add(new("COMMERCIAL_PACKAGE_CONFIGURATION_INCOMPLETE", "Informe unidade comercial PCT, unidade logística CX e fator de pacotes por caixa."));
        if (!product.IsWmcActive)
            issues.Add(new("WMC_PRODUCT_INACTIVE", "O produto está inativo no WMC."));
        if (!product.WmcStockAvailable || inventory is null || inventory.QuantityOnHand - inventory.QuantityReserved < 1)
            issues.Add(new("WMC_STOCK_UNAVAILABLE", "O produto precisa ter ao menos um pacote disponível no estoque WMC."));
        return issues;
    }

    private static bool IsAcceptedStagingUnit(string code, string unit) =>
        string.Equals(unit, "PCT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, Product504342Code, StringComparison.Ordinal)
            && (string.Equals(unit, "PC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(unit, "UN", StringComparison.OrdinalIgnoreCase));

    public static bool IsValidBarcode(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var validationResults = new List<ValidationResult>();
        return Validator.TryValidateValue(
            value.Trim(),
            new ValidationContext(new object()),
            validationResults,
            [new Gs1BarcodeAttribute(length)]);
    }
}

public sealed record WmcBimboValidationIssue(string Code, string Message);

public sealed record WmcBimboProductSimulation(
    string WmcCode,
    string? Description,
    string? Ean13,
    string SalesUnitCode,
    decimal? Stock,
    bool CanSelect,
    bool AlreadyImported,
    IReadOnlyList<WmcBimboValidationIssue> Issues,
    IReadOnlyList<string> SkipReasons);

public sealed record WmcBimboImportSimulationResult(
    bool Succeeded,
    IReadOnlyList<WmcBimboProductSimulation> Products,
    string? ErrorCode);

public sealed record WmcBimboAdminProductRow(
    string WmcCode,
    int? ProductId,
    string? Name,
    string? Ean13,
    string? Dun14,
    decimal? BasePrice,
    decimal? AvailableStock,
    string CommercialStatus,
    bool CanActivate,
    IReadOnlyList<string> Reasons);
