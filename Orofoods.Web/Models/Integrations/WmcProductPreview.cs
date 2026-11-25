namespace Orofoods.Web.Models.Integrations;

public sealed record WmcBrandRecord(int Code, string Description, int ProductCount, int ActiveProductCount);

public sealed record WmcProductPreviewResult(
    int ProductsRead,
    int NewProducts,
    int ExistingProducts,
    int ActiveProducts,
    int InactiveProducts,
    int UnknownSituations,
    int InvalidCodes,
    int DuplicateCodes,
    int StockProblems,
    int ProjectedAvailableProducts,
    int ProjectedBlockedProducts,
    int ActiveEligibleProducts,
    int InactiveIgnoredProducts,
    int InvalidSituations,
    int NewActiveProducts,
    int ExistingActiveProducts,
    string? ErrorMessage = null);

public sealed record WmcPreviewPageModel(
    IReadOnlyList<WmcBrandRecord> Brands,
    IReadOnlyList<short> SelectedBrandCodes,
    WmcProductPreviewResult? Preview,
    IReadOnlyList<WmcBrandPreviewResult> BrandPreviews,
    string Step = "Brands",
    short? SelectedBrandCode = null,
    string? SelectedBrandDescription = null,
    IReadOnlyList<WmcProductImportCandidate>? Candidates = null,
    IReadOnlyList<string>? SelectedProductCodes = null,
    WmcProductImportSummary? Confirmation = null,
    WmcProductImportResult? ImportResult = null,
    DateTime? LastProductSyncAt = null,
    IReadOnlyList<string>? SelectedProductKeys = null,
    string ProductSituationFilter = WmcProductSituationFilter.Active,
    WmcProductImportCounters? ProductCounters = null);

public sealed record WmcBrandPreviewResult(
    short Code,
    string Description,
    WmcProductPreviewResult Preview,
    IReadOnlyList<WmcProductPreviewItem> Products);

public sealed record WmcProductPreviewItem(
    string WmcCode,
    string Description,
    decimal? Stock,
    string Status);

public sealed record WmcProductPreviewBatchResult(
    WmcProductPreviewResult Summary,
    IReadOnlyList<WmcBrandPreviewResult> ByBrand);

public sealed record WmcProductUnitPreview(
    string UnitCode,
    string? Description,
    decimal? Factor,
    string? Barcode,
    bool IsMaster);

public sealed record WmcProductUnitImportProposal(
    string? PrimaryUnitCode,
    string? MasterUnitCode,
    string? CurrentEan,
    string? ProposedEan,
    string? CurrentEan13,
    string? ProposedEan13,
    string? CurrentDun14,
    string? ProposedDun14,
    decimal? CurrentPrimaryFactor,
    decimal? ProposedPrimaryFactor,
    decimal? CurrentMasterFactor,
    decimal? ProposedMasterFactor,
    decimal? CurrentPackageQuantity,
    decimal? ProposedPackageQuantity,
    decimal? CurrentConversionQuantity,
    decimal? ProposedConversionQuantity,
    bool SafeToApply,
    string? BlockReason)
{
    public decimal? CurrentPcFactor => string.Equals(PrimaryUnitCode, "PC", StringComparison.OrdinalIgnoreCase) ? CurrentPrimaryFactor : null;
    public decimal? ProposedPcFactor => string.Equals(PrimaryUnitCode, "PC", StringComparison.OrdinalIgnoreCase) ? ProposedPrimaryFactor : null;
    public decimal? CurrentCxFactor => string.Equals(MasterUnitCode, "CX", StringComparison.OrdinalIgnoreCase) ? CurrentMasterFactor : null;
    public decimal? ProposedCxFactor => string.Equals(MasterUnitCode, "CX", StringComparison.OrdinalIgnoreCase) ? ProposedMasterFactor : null;
}

public sealed record WmcProductPriceImportProposal(
    decimal? CurrentBasePrice,
    decimal? CurrentTargetPrice,
    decimal? ProposedPrice,
    string? SourceUnitCode,
    int? TargetPriceTableId,
    string? TargetPriceTableName,
    bool SafeToApply,
    string? BlockReason);

public static class WmcProductSituationFilter
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string All = "all";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Inactive => Inactive,
        All => All,
        _ => Active
    };
}

public sealed record WmcProductImportCounters(
    int Total,
    int Active,
    int Inactive,
    int Eligible,
    int Blocked)
{
    public static WmcProductImportCounters From(IEnumerable<WmcProductImportCandidate> candidates)
    {
        var rows = candidates.ToArray();
        return new(
            rows.Length,
            rows.Count(candidate => candidate.SituationKind == WmcProductSituationFilter.Active),
            rows.Count(candidate => candidate.SituationKind == WmcProductSituationFilter.Inactive),
            rows.Count(candidate => candidate.Selectable),
            rows.Count(candidate => !candidate.Selectable));
    }
}

public sealed record WmcProductImportCandidate(
    string WmcCode,
    string Name,
    string? Ean,
    string? Situation,
    string? BrandCode,
    string? BrandName,
    string? Unit,
    int? ProductId,
    string Status,
    bool Selectable,
    IReadOnlyList<WmcProductFieldChange> Changes,
    IReadOnlyList<WmcProductUnitPreview> Units,
    decimal? Stock = null,
    decimal? PrecoVenda = null,
    decimal? LocalBasePrice = null,
    string? LocalCommercialUnit = null,
    WmcProductUnitImportProposal? UnitProposal = null,
    WmcProductPriceImportProposal? PriceProposal = null,
    string? BlockReason = null,
    string SituationKind = "unknown");

public sealed record WmcProductFieldChange(string Field, string CurrentValue, string WmcValue);

public sealed record WmcProductImportSummary(
    int Consulted,
    int Selected,
    int Blocked,
    int NewProducts,
    int ExistingWithChanges,
    int ExistingWithoutChanges)
{
    public WmcProductImportSummary(
        int selected,
        int newProducts,
        int existingProducts,
        int changedProducts,
        int unchangedProducts)
        : this(selected, selected, 0, newProducts, changedProducts, unchangedProducts)
    {
    }

    public int ExistingProducts => ExistingWithChanges + ExistingWithoutChanges;
    public int ChangedProducts => ExistingWithChanges;
    public int UnchangedProducts => ExistingWithoutChanges;

    public static WmcProductImportSummary From(
        IEnumerable<WmcProductImportCandidate> candidates,
        IEnumerable<WmcProductImportCandidate> selected)
    {
        var consulted = candidates.ToArray();
        var selectedProducts = selected.ToArray();

        return new(
            consulted.Length,
            selectedProducts.Length,
            consulted.Count(candidate => !candidate.Selectable),
            selectedProducts.Count(candidate => candidate.ProductId is null),
            selectedProducts.Count(candidate => candidate.ProductId is not null && candidate.Changes.Count > 0),
            selectedProducts.Count(candidate => candidate.ProductId is not null && candidate.Changes.Count == 0));
    }
}

public sealed record WmcProductImportItemResult(string WmcCode, string Status, string? Detail);

public sealed record WmcProductImportResult(
    int Selected,
    int Created,
    int Updated,
    int Unchanged,
    int Errors,
    string Status,
    string? ErrorMessage,
    IReadOnlyList<WmcProductImportItemResult> Items,
    DateTime CompletedAt,
    int Blocked = 0);
