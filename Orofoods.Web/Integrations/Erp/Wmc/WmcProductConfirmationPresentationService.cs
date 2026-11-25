using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcProductConfirmationPresentationService(
    ApplicationDbContext db,
    IWmcProductReader reader)
{
    private const string WmcSource = "Consulta WMC somente leitura";
    private const string UnknownUnitSemantics = "não confirmada";

    public async Task<IReadOnlyList<WmcProductConfirmationPresentation>> BuildAsync(
        IReadOnlyCollection<short> brandCodes,
        IReadOnlyList<WmcProductImportCandidate> selectedCandidates,
        CancellationToken cancellationToken = default)
    {
        if (selectedCandidates.Count == 0) return [];

        var selectedBrands = brandCodes.Distinct().ToArray();
        var consultedAt = DateTimeOffset.UtcNow;
        var rowsByKey = new Dictionary<string, WmcProductRecord>(StringComparer.Ordinal);
        var failedBrands = new HashSet<short>();

        foreach (var brandCode in selectedBrands)
        {
            try
            {
                var rows = await reader.GetAllByBrandAsync(brandCode, cancellationToken);
                foreach (var row in rows)
                {
                    var code = WmcProductRules.NormalizeCode(row.CodProduto);
                    if (code.Length > 0)
                        rowsByKey[$"{brandCode}:{code}"] = row;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedBrands.Add(brandCode);
            }
        }

        var localStates = await ReadLocalStatesAsync(selectedCandidates, cancellationToken);
        return selectedCandidates
            .Select(candidate => BuildPresentation(candidate, selectedBrands, rowsByKey, failedBrands, localStates, consultedAt))
            .ToArray();
    }

    private async Task<LocalReadResult> ReadLocalStatesAsync(
        IReadOnlyList<WmcProductImportCandidate> selectedCandidates,
        CancellationToken cancellationToken)
    {
        var productIds = selectedCandidates
            .Where(candidate => candidate.ProductId.HasValue)
            .Select(candidate => candidate.ProductId!.Value)
            .Distinct()
            .ToArray();
        if (productIds.Length == 0)
            return new(new Dictionary<int, LocalProductSnapshot>(), new Dictionary<int, ProductInventorySnapshot>(), false);

        try
        {
            var products = await db.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id))
                .Select(product => new LocalProductSnapshot(
                    product.Id,
                    product.Sku,
                    product.Name,
                    product.Unit,
                    product.CommercialUnitCode,
                    product.LogisticsUnitCode,
                    product.WmcAlternateUnit,
                    product.WmcPackageQuantity,
                    product.WmcConversionQuantity,
                    product.Ean,
                    product.Ean13,
                    product.Dun14,
                    product.IsWmcActive,
                    product.IsActive,
                    product.IsAvailable,
                    product.WmcInitialLoadReady,
                    product.WmcStockAvailable))
                .ToDictionaryAsync(product => product.ProductId, cancellationToken);
            var inventories = await db.ProductInventories
                .AsNoTracking()
                .Where(inventory => productIds.Contains(inventory.ProductId))
                .Select(inventory => new ProductInventorySnapshot(
                    inventory.ProductId,
                    inventory.QuantityOnHand,
                    inventory.QuantityReserved,
                    inventory.IsManuallyAdjusted))
                .ToDictionaryAsync(inventory => inventory.ProductId, cancellationToken);
            return new(products, inventories, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(new Dictionary<int, LocalProductSnapshot>(), new Dictionary<int, ProductInventorySnapshot>(), true);
        }
    }

    private static WmcProductConfirmationPresentation BuildPresentation(
        WmcProductImportCandidate candidate,
        IReadOnlyCollection<short> selectedBrands,
        IReadOnlyDictionary<string, WmcProductRecord> rowsByKey,
        IReadOnlySet<short> failedBrands,
        LocalReadResult localStates,
        DateTimeOffset consultedAt)
    {
        var brandCode = short.TryParse(candidate.BrandCode, out var parsedBrandCode)
            ? parsedBrandCode
            : selectedBrands.Count == 1 ? selectedBrands.Single() : (short?)null;
        var code = WmcProductRules.NormalizeCode(candidate.WmcCode);
        var key = brandCode.HasValue ? $"{brandCode.Value}:{code}" : null;
        var isNewProduct = candidate.ProductId is null;
        var blockers = new List<string>();
        if (!string.IsNullOrWhiteSpace(candidate.BlockReason))
            blockers.Add(candidate.BlockReason);

        WmcProductConfirmationWmcStock? wmcStock = null;
        var wmcReadStatus = "not-found";
        if (key is not null && rowsByKey.TryGetValue(key, out var row))
        {
            wmcReadStatus = "available";
            wmcStock = new(
                row.EstoqueDisponivel,
                row.EstoqueAtual,
                row.EstoqueReservado,
                WmcSource,
                consultedAt,
                UnknownUnitSemantics);
        }
        else if (brandCode.HasValue && failedBrands.Contains(brandCode.Value))
        {
            wmcReadStatus = "unavailable";
        }

        WmcProductConfirmationLocalState? localState = null;
        var localReadStatus = isNewProduct
            ? "not-applicable"
            : localStates.ReadFailed
                ? "unavailable"
                : "not-found";
        if (candidate.ProductId is int productId && localStates.Products.TryGetValue(productId, out var product))
        {
            localStates.Inventories.TryGetValue(productId, out var inventory);
            localReadStatus = "available";
            localState = new(
                product.ProductId,
                product.Sku,
                product.Name,
                product.Unit,
                product.CommercialUnitCode,
                product.LogisticsUnitCode,
                product.WmcAlternateUnit,
                product.WmcPackageQuantity,
                product.WmcConversionQuantity,
                product.Ean,
                product.Ean13,
                product.Dun14,
                product.IsWmcActive,
                product.IsActive,
                product.IsAvailable,
                product.WmcInitialLoadReady,
                product.WmcStockAvailable,
                inventory?.QuantityOnHand,
                inventory?.QuantityReserved,
                inventory is null ? null : Math.Max(0, inventory.QuantityOnHand - inventory.QuantityReserved),
                inventory?.IsManuallyAdjusted,
                inventory is null ? "not-informed" : "available");

            if (!product.IsWmcActive) blockers.Add("Situação WMC no cadastro está inativa.");
            if (!product.IsActive) blockers.Add("Cadastro Orofoods está inativo.");
            if (!product.IsAvailable) blockers.Add("Disponibilidade comercial está desativada.");
            if (!product.WmcInitialLoadReady) blockers.Add("Carga inicial WMC ainda não está liberada.");
            if (!product.WmcStockAvailable) blockers.Add("Estoque WMC não está disponível.");
            if (inventory?.IsManuallyAdjusted == true) blockers.Add("Estoque local possui ajuste manual; não foi alterado.");
        }
        else if (!isNewProduct && !localStates.ReadFailed)
        {
            blockers.Add("Cadastro Orofoods não localizado para a seleção.");
        }

        return new(
            candidate,
            isNewProduct,
            wmcStock,
            localState,
            wmcReadStatus,
            localReadStatus,
            blockers.Distinct(StringComparer.Ordinal).ToArray(),
            isNewProduct
                ? "Após a importação manual, IsActive=true para SITUACAO='A'; IsAvailable=false até o fluxo comercial autorizado; nenhum estoque local é criado."
                : null);
    }

    private sealed record LocalReadResult(
        IReadOnlyDictionary<int, LocalProductSnapshot> Products,
        IReadOnlyDictionary<int, ProductInventorySnapshot> Inventories,
        bool ReadFailed);

    private sealed record LocalProductSnapshot(
        int ProductId,
        string Sku,
        string Name,
        string Unit,
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
        bool WmcStockAvailable);

    private sealed record ProductInventorySnapshot(
        int ProductId,
        int QuantityOnHand,
        int QuantityReserved,
        bool IsManuallyAdjusted);
}
