namespace Orofoods.Web.Models.Integrations;

public sealed record WmcBrandRecord(int Code, string Description, int ProductCount);

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
    short? SelectedBrandCode,
    WmcProductPreviewResult? Preview);
