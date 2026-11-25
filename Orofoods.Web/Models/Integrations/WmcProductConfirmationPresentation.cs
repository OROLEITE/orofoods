namespace Orofoods.Web.Models.Integrations;

public sealed record WmcProductConfirmationPresentation(
    WmcProductImportCandidate Candidate,
    bool IsNewProduct,
    WmcProductConfirmationWmcStock? WmcStock,
    WmcProductConfirmationLocalState? LocalState,
    string WmcReadStatus,
    string LocalReadStatus,
    IReadOnlyList<string> KnownBlockers,
    string? NewProductExpectedState);

public sealed record WmcProductConfirmationWmcStock(
    decimal? EstoqueDisponivel,
    decimal? EstoqueAtual,
    decimal? EstoqueReservado,
    string Source,
    DateTimeOffset ConsultedAtUtc,
    string UnitSemantics);

public sealed record WmcProductConfirmationLocalState(
    int ProductId,
    string? Sku,
    string? Name,
    string? Unit,
    string? CommercialUnitCode,
    string? LogisticsUnitCode,
    string? WmcAlternateUnit,
    decimal? WmcPackageQuantity,
    decimal? WmcConversionQuantity,
    string? Ean,
    string? Ean13,
    string? Dun14,
    bool IsWmcActive,
    bool IsActive,
    bool IsAvailable,
    bool WmcInitialLoadReady,
    bool WmcStockAvailable,
    int? QuantityOnHand,
    int? QuantityReserved,
    int? AvailableQuantity,
    bool? IsManuallyAdjusted,
    string InventoryReadStatus);
