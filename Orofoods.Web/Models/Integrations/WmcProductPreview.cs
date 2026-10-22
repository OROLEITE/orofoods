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
    DateTime? LastProductSyncAt = null);

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
    string? LocalCommercialUnit = null);

public sealed record WmcProductFieldChange(string Field, string CurrentValue, string WmcValue);

public sealed record WmcProductImportSummary(
    int Selected,
    int NewProducts,
    int ExistingProducts,
    int ChangedProducts,
    int UnchangedProducts);

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
    DateTime CompletedAt);
