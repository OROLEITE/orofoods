using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Pricing;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcProductImportService(
    ApplicationDbContext db,
    IWmcProductReader reader,
    IOptions<WmcPriceImportOptions>? priceOptions = null)
{
    private readonly WmcPriceImportOptions priceOptions = priceOptions?.Value ?? new();

    public Task<IReadOnlyList<WmcProductImportCandidate>> GetCandidatesAsync(short brandCode, CancellationToken ct = default) =>
        GetCandidatesAsync((IReadOnlyCollection<short>)[brandCode], ct);

    public async Task<IReadOnlyList<WmcProductImportCandidate>> GetCandidatesAsync(IReadOnlyCollection<short> brandCodes, CancellationToken ct = default)
    {
        var selectedBrands = brandCodes.Distinct().ToArray();
        if (selectedBrands.Length == 0) return [];
        var source = new List<(short BrandCode, WmcProductRecord Row)>();
        foreach (var brandCode in selectedBrands)
        {
            var rows = await reader.GetAllByBrandWithUnitsAsync(brandCode, ct);
            source.AddRange(rows.Select(row => (brandCode, row)));
        }
        var products = await db.Products.AsNoTracking().Where(p => p.WmcCode != null).ToListAsync(ct);
        var local = products.GroupBy(p => WmcProductRules.NormalizeCode(p.WmcCode), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var duplicateKeys = source
            .GroupBy(item => (item.BrandCode, Code: WmcProductRules.NormalizeCode(item.Row.CodProduto)))
            .Where(group => group.Key.Code.Length == 0 || group.Count() > 1)
            .Select(group => $"{group.Key.BrandCode}:{group.Key.Code}")
            .ToHashSet(StringComparer.Ordinal);
        var crossBrandCodes = source
            .Where(item => WmcProductRules.NormalizeCode(item.Row.CodProduto).Length > 0)
            .GroupBy(item => WmcProductRules.NormalizeCode(item.Row.CodProduto), StringComparer.Ordinal)
            .Where(group => group.Select(item => item.BrandCode).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var productIds = products.Select(product => product.Id).ToArray();
        var unitsByProduct = await db.WmcProductUnits.AsNoTracking()
            .Where(unit => productIds.Contains(unit.ProductId))
            .ToListAsync(ct);
        var groupedUnits = unitsByProduct.GroupBy(unit => unit.ProductId).ToDictionary(group => group.Key, group => group.ToArray());
        var targetPriceTable = await LoadTargetPriceTableAsync(ct);
        var targetPrices = targetPriceTable is null
            ? new Dictionary<int, decimal>()
            : await db.PriceTableItems.AsNoTracking()
                .Where(item => item.PriceTableId == targetPriceTable.Id)
                .ToDictionaryAsync(item => item.ProductId, item => item.Price, ct);

        return source.Select(sourceItem =>
        {
            var brandCode = sourceItem.BrandCode;
            var row = sourceItem.Row;
            var code = WmcProductRules.NormalizeCode(row.CodProduto);
            local.TryGetValue(code, out var matches);
            var product = matches?.Count == 1 ? matches[0] : null;
            var currentUnits = product is not null && groupedUnits.TryGetValue(product.Id, out var productUnits)
                ? productUnits
                : [];
            var sourceUnits = row.Units ?? [];
            var unitProposal = BuildUnitProposal(product, currentUnits, row, row.UnitsSnapshotComplete);
            var currentTargetPrice = product is not null && targetPrices.TryGetValue(product.Id, out var targetPrice)
                ? targetPrice
                : (decimal?)null;
            var priceProposal = BuildPriceProposal(product, row, targetPriceTable, currentTargetPrice);
            var situation = WmcProductRules.ClassifySituation(row.Situacao);
            var changes = product is null
                ? Array.Empty<WmcProductFieldChange>()
                : GetChanges(product, row, unitProposal, priceProposal)
                    .Concat(unitProposal.SafeToApply && sourceUnits.Count > 0
                        ? GetUnitChanges(currentUnits, sourceUnits)
                        : [])
                    .ToArray();
            var invalidUnitSnapshot = !row.UnitsSnapshotComplete
                || row.Units is null
                || sourceUnits.Count == 0
                || !TryToSourceUnitDictionary(sourceUnits, out _);
            var duplicate = string.IsNullOrWhiteSpace(code)
                || duplicateKeys.Contains($"{brandCode}:{code}")
                || crossBrandCodes.Contains(code)
                || (matches?.Count ?? 0) > 1
                || (product is null && string.IsNullOrWhiteSpace(row.Produto))
                || invalidUnitSnapshot;
            var changed = changes.Length > 0;
            var inactiveSituation = situation == WmcProductSituation.Inactive;
            var invalidSituation = situation == WmcProductSituation.Unknown;
            var status = duplicate ? "Inválido" : product is null ? "Novo" : changed ? "Com alterações" : "Já cadastrado";
            if (!unitProposal.SafeToApply) status += $" · unidades bloqueadas: {unitProposal.BlockReason}";
            if (!priceProposal.SafeToApply) status += $" · preço bloqueado: {priceProposal.BlockReason}";
            if (invalidSituation) status += " · situação inválida; venda bloqueada ao reimportar";
            var blockReasons = new List<string>();
            if (inactiveSituation) blockReasons.Add("Produto inativo no WMC; somente consulta.");
            if (string.IsNullOrWhiteSpace(code)) blockReasons.Add("código WMC ausente");
            if (duplicateKeys.Contains($"{brandCode}:{code}")) blockReasons.Add("código WMC duplicado na marca");
            if (crossBrandCodes.Contains(code)) blockReasons.Add("código WMC aparece em mais de uma marca selecionada");
            if ((matches?.Count ?? 0) > 1) blockReasons.Add("código WMC duplicado no cadastro local");
            if (product is null && string.IsNullOrWhiteSpace(row.Produto)) blockReasons.Add("descrição WMC ausente para novo produto");
            if (invalidUnitSnapshot && unitProposal.BlockReason is { } unitReason) blockReasons.Add(unitReason);
            if (!unitProposal.SafeToApply && unitProposal.BlockReason is { } blockedUnitReason
                && !blockReasons.Contains(blockedUnitReason, StringComparer.Ordinal))
                blockReasons.Add($"unidades WMC bloqueadas: {blockedUnitReason}");
            if (!priceProposal.SafeToApply && priceProposal.BlockReason is { } blockedPriceReason)
                blockReasons.Add($"preço WMC bloqueado: {blockedPriceReason}");
            if (invalidSituation) blockReasons.Add("situação WMC inválida");
            var blocked = blockReasons.Count > 0;
            var localCommercialUnit = product is null
                ? null
                : string.IsNullOrWhiteSpace(product.CommercialUnitCode) ? product.Unit : product.CommercialUnitCode;
            return new WmcProductImportCandidate(
                code,
                row.Produto,
                unitProposal.ProposedEan ?? Clean(row.Ean),
                row.Situacao,
                brandCode.ToString(),
                Clean(row.Marca),
                Clean(row.Un),
                product?.Id,
                status,
                !blocked && !duplicate,
                changes,
                sourceUnits
                    .OrderBy(unit => NormalizeUnitCode(unit.UnitCode), StringComparer.Ordinal)
                    .Select(unit => new WmcProductUnitPreview(
                        NormalizeUnitCode(unit.UnitCode),
                        Clean(unit.Description) ?? "",
                        unit.Factor,
                        Clean(unit.Barcode),
                        unit.IsMaster))
                    .ToArray(),
                Stock: row.EstoqueDisponivel,
                PrecoVenda: row.PrecoVenda,
                LocalBasePrice: product?.BasePrice,
                LocalCommercialUnit: localCommercialUnit,
                UnitProposal: unitProposal,
                PriceProposal: priceProposal,
                BlockReason: blockReasons.Count == 0 ? null : string.Join("; ", blockReasons),
                SituationKind: situation switch
                {
                    WmcProductSituation.Active => WmcProductSituationFilter.Active,
                    WmcProductSituation.Inactive => WmcProductSituationFilter.Inactive,
                    _ => "unknown"
                });
        }).ToArray();
    }

    public async Task<WmcProductImportResult> ImportAsync(short brandCode, IReadOnlyCollection<string> selectedCodes, bool reimportExisting, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var codes = selectedCodes.Select(WmcProductRules.NormalizeCode).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var itemResults = new List<WmcProductImportItemResult>();
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var blockedCodes = new HashSet<string>(StringComparer.Ordinal);
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var rows = await reader.GetAllByBrandWithUnitsAsync(brandCode, ct);
            var dups = WmcProductRules.FindDuplicateCodes(rows).ToHashSet(StringComparer.Ordinal);
            var byCode = rows.Where(r => !string.IsNullOrWhiteSpace(r.CodProduto))
                .GroupBy(r => WmcProductRules.NormalizeCode(r.CodProduto), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            if (codes.Length == 0 || codes.Length != selectedCodes.Count
                || codes.Any(code => !byCode.TryGetValue(code, out var found) || found.Length != 1 || dups.Contains(code)))
                throw new InvalidOperationException("A seleção mudou ou contém produto inválido/duplicado. Atualize a prévia.");

            var requestedRows = codes.Select(code => byCode[code][0]).ToArray();
            if (requestedRows.Any(row => WmcProductRules.ClassifySituation(row.Situacao) == WmcProductSituation.Inactive))
                throw new InvalidOperationException("A importação manual não aceita produtos WMC inativos (SITUACAO = 'I').");

            var unitsByCode = requestedRows.ToDictionary(
                row => WmcProductRules.NormalizeCode(row.CodProduto),
                row => ReadAndValidateUnits(row),
                StringComparer.Ordinal);
            var trackedProducts = await db.Products.Where(p => p.WmcCode != null).ToListAsync(ct);
            var localGroups = trackedProducts
                .GroupBy(p => WmcProductRules.NormalizeCode(p.WmcCode), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            var selectedProductIds = requestedRows
                .Select(row => localGroups.TryGetValue(WmcProductRules.NormalizeCode(row.CodProduto), out var matches) && matches.Length == 1
                    ? matches[0].Id
                    : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();
            var existingUnits = await db.WmcProductUnits
                .Where(unit => selectedProductIds.Contains(unit.ProductId))
                .ToListAsync(ct);
            var existingUnitsByProduct = existingUnits.GroupBy(unit => unit.ProductId).ToDictionary(group => group.Key, group => group.ToList());
            var targetPriceTable = await LoadTargetPriceTableAsync(ct, tracked: true);
            var targetItems = targetPriceTable is null
                ? []
                : await db.PriceTableItems.Where(item => item.PriceTableId == targetPriceTable.Id).ToListAsync(ct);
            var inventories = selectedProductIds.Length == 0
                ? new Dictionary<int, ProductInventory>()
                : await db.ProductInventories
                    .Where(inventory => selectedProductIds.Contains(inventory.ProductId))
                    .ToDictionaryAsync(inventory => inventory.ProductId, ct);
            var pending = await db.ProductCategories.FirstOrDefaultAsync(c => c.Slug == "wmc-pending-import", ct);
            if (pending is null)
            {
                pending = new ProductCategory { Name = "A categorizar (WMC)", Slug = "wmc-pending-import", IsActive = false };
                db.ProductCategories.Add(pending);
                await db.SaveChangesAsync(ct);
            }

            foreach (var row in requestedRows)
            {
                var code = WmcProductRules.NormalizeCode(row.CodProduto);
                localGroups.TryGetValue(code, out var matches);
                if ((matches?.Length ?? 0) > 1) throw new InvalidOperationException($"WmcCode duplicado localmente: {code}.");
                var product = matches?.SingleOrDefault();
                if (product is not null && !reimportExisting)
                {
                    unchanged++;
                    itemResults.Add(new(code, "Ignorado", "Produto existente; reimportação recusada."));
                    continue;
                }

                var isNew = product is null;
                if (isNew)
                {
                    if (string.IsNullOrWhiteSpace(row.Produto)) throw new InvalidOperationException($"Nome WMC ausente para novo produto {code}.");
                    product = new Product
                    {
                        WmcCode = code,
                        Sku = PlaceholderSku(code),
                        Name = "",
                        ProductCategoryId = pending.Id,
                        IsActive = false,
                        IsAvailable = false,
                        IsWmcActive = false,
                        WmcStockAvailable = false
                    };
                    db.Products.Add(product);
                    created++;
                }

                var current = product!;
                var officialUnits = unitsByCode[code];
                var productUnits = !isNew && existingUnitsByProduct.TryGetValue(current.Id, out var localUnits)
                    ? localUnits
                    : [];
                var unitProposal = BuildUnitProposal(current, productUnits, row, row.UnitsSnapshotComplete);
                var currentTargetPrice = targetItems.FirstOrDefault(item => item.ProductId == current.Id)?.Price;
                var priceProposal = BuildPriceProposal(current, row, targetPriceTable, currentTargetPrice);
                var priceChanged = priceProposal.SafeToApply
                    && priceProposal.ProposedPrice is decimal proposedPriceValue
                    && current.BasePrice != proposedPriceValue;
                var before = GetChanges(current, row, unitProposal, priceProposal).Any()
                    || priceChanged
                    || (unitProposal.SafeToApply && officialUnits.Count > 0 && !UnitSetsEqual(productUnits, officialUnits));
                var beforeBasePrice = current.BasePrice;
                var beforeTargetPrice = currentTargetPrice;
                ApplyMappedFields(current, row, unitProposal);
                if (unitProposal.SafeToApply)
                    ApplyOfficialUnitFields(current, unitProposal);
                if (unitProposal.SafeToApply && officialUnits.Count > 0)
                    ReconcileUnits(current, productUnits, officialUnits, isNew);

                var detail = new List<string>();
                if (!unitProposal.SafeToApply)
                {
                    blockedCodes.Add(code);
                    detail.Add($"Unidades bloqueadas: {unitProposal.BlockReason}");
                }
                if (priceProposal.SafeToApply && priceProposal.ProposedPrice is decimal proposedPrice)
                {
                    ApplyValidatedPrice(current, proposedPrice);
                    if (beforeBasePrice != proposedPrice || beforeTargetPrice != proposedPrice)
                        detail.Add($"Preço atualizado: BasePrice {beforeBasePrice:0.##} → {proposedPrice:0.##}");
                }
                else
                {
                    blockedCodes.Add(code);
                    detail.Add($"Preço bloqueado: {priceProposal.BlockReason}");
                }
                if (unitProposal.SafeToApply && HasUnitFieldChanges(unitProposal))
                    detail.Add("Identificadores, fatores e quantidades oficiais aplicados.");

                var inventory = inventories.GetValueOrDefault(current.Id);
                var stockValid = WmcProductRules.TryNormalizeStock(row.EstoqueDisponivel, out var availableQuantity, out _);
                if (stockValid && inventory?.IsManuallyAdjusted != true)
                    current.WmcStockAvailable = current.IsWmcActive && availableQuantity > 0;
                var effectiveAvailableQuantity = inventory is null
                    ? availableQuantity
                    : Math.Min(availableQuantity, inventory.AvailableQuantity);
                var readiness = stockValid
                    ? WmcManualProductImportReadinessEvaluator.Evaluate(
                        source: row,
                        product: current,
                        units: officialUnits,
                        unitsSnapshotComplete: row.UnitsSnapshotComplete,
                        effectiveAvailableQuantity,
                        inventory?.IsManuallyAdjusted == true)
                    : WmcInitialLoadReadinessResult.Blocked("stock-invalid");
                current.WmcInitialLoadReady = readiness.IsReady;
                detail.Add(readiness.IsReady
                    ? "Liberação automática WMC aplicada."
                    : $"Prontidão WMC bloqueada: {readiness.Reason}.");

                if (!isNew && before) updated++;
                else if (!isNew) unchanged++;
                var status = isNew ? "Criado" : before ? "Atualizado" : "Sem alterações";
                itemResults.Add(new(code, status, detail.Count == 0 ? null : string.Join(" ", detail)));
                if (matches is null) localGroups[code] = [current];
            }

            var completed = DateTime.UtcNow;
            var result = new WmcProductImportResult(codes.Length, created, updated, unchanged, 0, "Succeeded", null, itemResults, completed, blockedCodes.Count);
            db.WmcProductImportAudits.Add(Audit(brandCode, started, result));
            await db.SaveChangesAsync(ct);
            if (ownsTransaction) await tx!.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ownsTransaction) await tx!.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            if (!ownsTransaction) throw;
            return await RecordFailureAsync(brandCode, codes.Length, started, ex.Message, ct);
        }
    }

    public async Task<WmcProductImportResult> ImportAsync(
        IReadOnlyDictionary<short, IReadOnlyCollection<string>> selections,
        bool reimportExisting,
        CancellationToken ct = default)
    {
        var normalized = selections
            .Where(item => item.Value.Count > 0)
            .ToDictionary(item => item.Key,
                item => (IReadOnlyCollection<string>)item.Value
                    .Select(WmcProductRules.NormalizeCode)
                    .Where(code => code.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
        if (normalized.Count == 0)
            return new WmcProductImportResult(0, 0, 0, 0, 0, "Failed", "Nenhum produto elegível foi selecionado.", [], DateTime.UtcNow);

        var allCandidates = await GetCandidatesAsync(normalized.Keys.ToArray(), ct);
        var requestedKeys = normalized.SelectMany(item => item.Value.Select(code => $"{item.Key}:{code}")).ToHashSet(StringComparer.Ordinal);
        var validCandidates = allCandidates.Where(candidate => requestedKeys.Contains($"{candidate.BrandCode}:{candidate.WmcCode}")).ToArray();
        if (validCandidates.Length != requestedKeys.Count || validCandidates.Any(candidate => !candidate.Selectable))
            return new WmcProductImportResult(requestedKeys.Count, 0, 0, 0, requestedKeys.Count, "Failed",
                "A seleção contém produtos bloqueados ou desatualizados. Atualize a prévia.", [], DateTime.UtcNow);

        if (normalized.Count == 1)
        {
            var single = normalized.Single();
            return await ImportAsync(single.Key, single.Value, reimportExisting, ct);
        }

        var started = DateTime.UtcNow;
        short? currentBrand = null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var results = new List<WmcProductImportResult>();
            foreach (var item in normalized.OrderBy(item => item.Key))
            {
                currentBrand = item.Key;
                var result = await ImportAsync(item.Key, item.Value, reimportExisting, ct);
                if (result.Status != "Succeeded") throw new InvalidOperationException(result.ErrorMessage ?? "A importação de uma marca falhou.");
                results.Add(result);
            }

            await tx.CommitAsync(ct);
            return new WmcProductImportResult(
                results.Sum(result => result.Selected),
                results.Sum(result => result.Created),
                results.Sum(result => result.Updated),
                results.Sum(result => result.Unchanged),
                results.Sum(result => result.Errors),
                "Succeeded", null,
                results.SelectMany(result => result.Items).ToArray(),
                results.Max(result => result.CompletedAt),
                results.Sum(result => result.Blocked));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await RecordFailureAsync(currentBrand ?? normalized.Keys.Min(), requestedKeys.Count, started, ex.Message, ct);
        }
    }

    public async Task<DateTime?> GetLastProductImportAsync(CancellationToken ct = default)
    {
        var importAt = await db.WmcProductImportAudits.AsNoTracking().Where(a => a.Status == "Succeeded")
            .MaxAsync(a => (DateTime?)a.CompletedAt, ct);
        var syncAt = await db.WmcSyncRuns.AsNoTracking().Where(r => r.Status == "Succeeded" && r.ProductsRead > 0)
            .MaxAsync(r => (DateTime?)r.FinishedAt, ct);
        return new[] { importAt, syncAt }.Where(value => value.HasValue).Max();
    }

    private async Task<PriceTable?> LoadTargetPriceTableAsync(CancellationToken ct, bool tracked = false)
    {
        if (priceOptions.TargetPriceTableId is not > 0) return null;
        var query = db.PriceTables.AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(table => table.Id == priceOptions.TargetPriceTableId.Value, ct);
    }

    private WmcProductPriceImportProposal BuildPriceProposal(Product? product, WmcProductRecord row, PriceTable? targetTable, decimal? currentTargetPrice)
    {
        var reasons = new List<string>();
        var sourceUnit = Clean(row.Un);
        if (row.PrecoVenda is not > 0) reasons.Add("PRECOVENDA ausente, zero ou negativo.");
        return new(product?.BasePrice, currentTargetPrice, row.PrecoVenda, sourceUnit, targetTable?.Id, targetTable?.Name,
            reasons.Count == 0, reasons.Count == 0 ? null : string.Join(" ", reasons));
    }

    private static void ApplyValidatedPrice(Product product, decimal price)
    {
        product.BasePrice = price;
    }

    private async Task<WmcProductImportResult> RecordFailureAsync(short brand, int selected, DateTime started, string error, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var item = new WmcProductImportItemResult("", "Erro", error);
        var result = new WmcProductImportResult(selected, 0, 0, 0, selected, "Failed", error, [item], now);
        db.WmcProductImportAudits.Add(Audit(brand, started, result));
        await db.SaveChangesAsync(ct);
        return result;
    }

    private static WmcProductImportAudit Audit(short brand, DateTime started, WmcProductImportResult r) => new()
    {
        BrandCode = brand, StartedAt = started, CompletedAt = r.CompletedAt, Status = r.Status,
        Selected = r.Selected, Created = r.Created, Updated = r.Updated, Unchanged = r.Unchanged, Errors = r.Errors,
        ErrorMessage = r.ErrorMessage, ResultsJson = JsonSerializer.Serialize(r.Items)
    };

    private static IEnumerable<WmcProductFieldChange> GetChanges(Product product, WmcProductRecord row, WmcProductUnitImportProposal unitProposal, WmcProductPriceImportProposal priceProposal)
    {
        if (!string.IsNullOrWhiteSpace(row.Produto) && product.Name != row.Produto.Trim()) yield return new("Descrição", product.Name, row.Produto.Trim());
        if (Clean(row.CodMarca) is { } brand && product.WmcBrandCode != brand) yield return new("Código da marca", product.WmcBrandCode ?? "", brand);
        if (Clean(row.Marca) is { } brandName && product.Brand != brandName) yield return new("Marca", product.Brand, brandName);
        if (Clean(row.Un) is { } unit && product.Unit != unit) yield return new("Unidade", product.Unit, unit);
        if (Clean(row.UnAltern) is { } alt && product.WmcAlternateUnit != alt) yield return new("Unidade alternativa", product.WmcAlternateUnit ?? "", alt);
        if (priceProposal.SafeToApply && priceProposal.ProposedPrice is decimal proposedPrice && product.BasePrice != proposedPrice)
            yield return new("Preço base", product.BasePrice.ToString("0.##"), proposedPrice.ToString("0.##"));
        foreach (var change in GetUnitFieldChanges(unitProposal)) yield return change;
        var situation = WmcProductRules.ClassifySituation(row.Situacao);
        if (situation == WmcProductSituation.Active && !product.IsWmcActive) yield return new("Situação WMC", "Inativo", "Ativo");
        else if (situation == WmcProductSituation.Inactive && product.IsWmcActive) yield return new("Situação WMC", "Ativo", "Inativo");
        else if (situation == WmcProductSituation.Unknown && !string.IsNullOrWhiteSpace(row.Situacao) && product.IsWmcActive)
            yield return new("Situação WMC", "Ativo", "Bloqueado: situação inválida");
    }

    private static IEnumerable<WmcProductFieldChange> GetUnitFieldChanges(WmcProductUnitImportProposal proposal)
    {
        if (!proposal.SafeToApply) yield break;
        if (proposal.ProposedEan is { } ean && !string.Equals(proposal.CurrentEan, ean, StringComparison.Ordinal)) yield return new("EAN oficial da unidade principal", proposal.CurrentEan ?? "", ean);
        if (proposal.ProposedEan13 is { } ean13 && !string.Equals(proposal.CurrentEan13, ean13, StringComparison.Ordinal)) yield return new("EAN-13 oficial da unidade principal", proposal.CurrentEan13 ?? "", ean13);
        if (proposal.ProposedDun14 is { } dun14 && !string.Equals(proposal.CurrentDun14, dun14, StringComparison.Ordinal)) yield return new("DUN-14 oficial da unidade master", proposal.CurrentDun14 ?? "", dun14);
        if (proposal.ProposedPrimaryFactor is decimal primaryFactor && proposal.CurrentPrimaryFactor != primaryFactor)
            yield return new($"Fator oficial da {proposal.PrimaryUnitCode}", proposal.CurrentPrimaryFactor?.ToString() ?? "", primaryFactor.ToString());
        if (proposal.ProposedMasterFactor is decimal masterFactor && proposal.CurrentMasterFactor != masterFactor)
            yield return new($"Fator oficial da {proposal.MasterUnitCode}", proposal.CurrentMasterFactor?.ToString() ?? "", masterFactor.ToString());
        if (proposal.ProposedPackageQuantity is decimal packageQuantity && proposal.CurrentPackageQuantity != packageQuantity)
            yield return new("QTDE_EMB oficial", proposal.CurrentPackageQuantity?.ToString() ?? "", packageQuantity.ToString());
        if (proposal.ProposedConversionQuantity is decimal conversionQuantity && proposal.CurrentConversionQuantity != conversionQuantity)
            yield return new("QTDE_CONV oficial", proposal.CurrentConversionQuantity?.ToString() ?? "", conversionQuantity.ToString());
    }

    private static bool HasUnitFieldChanges(WmcProductUnitImportProposal proposal) => GetUnitFieldChanges(proposal).Any();

    private static IEnumerable<WmcProductFieldChange> GetUnitChanges(IReadOnlyCollection<WmcProductUnit> current, IReadOnlyCollection<WmcProductUnitRecord> source)
    {
        if (!UnitSetsEqual(current, source)) yield return new("Unidades oficiais WMC", Summarize(current), Summarize(source));
    }

    private void ReconcileUnits(Product product, List<WmcProductUnit> current, IReadOnlyList<WmcProductUnitRecord> source, bool isNew)
    {
        if (!TryToSourceUnitDictionary(source, out var sourceByCode)) return;
        var currentByCode = current.ToDictionary(unit => NormalizeUnitCode(unit.UnitCode), StringComparer.Ordinal);
        foreach (var (code, existing) in currentByCode)
        {
            if (!sourceByCode.ContainsKey(code)) db.WmcProductUnits.Remove(existing);
        }
        foreach (var (code, incoming) in sourceByCode)
        {
            if (currentByCode.TryGetValue(code, out var existing))
            {
                if (Clean(incoming.Description) is { } description) existing.Description = description;
                if (incoming.Factor is > 0) existing.Factor = incoming.Factor;
                if (IsValidUnitBarcode(incoming.Barcode)) existing.Barcode = Clean(incoming.Barcode);
                existing.IsMaster = incoming.IsMaster;
                continue;
            }
            var unit = new WmcProductUnit
            {
                UnitCode = code,
                Description = Clean(incoming.Description) ?? "",
                Factor = incoming.Factor is > 0 ? incoming.Factor : null,
                Barcode = IsValidUnitBarcode(incoming.Barcode) ? Clean(incoming.Barcode) : null,
                IsMaster = incoming.IsMaster
            };
            if (isNew) product.WmcUnits.Add(unit);
            else
            {
                unit.ProductId = product.Id;
                db.WmcProductUnits.Add(unit);
            }
        }
    }

    private static bool UnitSetsEqual(IEnumerable<WmcProductUnit> current, IEnumerable<WmcProductUnitRecord> source)
    {
        var currentByCode = current.ToDictionary(unit => NormalizeUnitCode(unit.UnitCode), StringComparer.Ordinal);
        if (!TryToSourceUnitDictionary(source, out var sourceByCode) || currentByCode.Count != sourceByCode.Count) return false;
        foreach (var (code, local) in currentByCode)
        {
            if (!sourceByCode.TryGetValue(code, out var official)
                || (Clean(official.Description) is { } description && !string.Equals(local.Description.Trim(), description, StringComparison.Ordinal))
                || (official.Factor is > 0 && local.Factor != official.Factor)
                || (IsValidUnitBarcode(official.Barcode) && !string.Equals(Clean(local.Barcode), Clean(official.Barcode), StringComparison.Ordinal))
                || local.IsMaster != official.IsMaster) return false;
        }
        return true;
    }

    private static bool TryToSourceUnitDictionary(IEnumerable<WmcProductUnitRecord> units, out Dictionary<string, WmcProductUnitRecord> result)
    {
        result = new Dictionary<string, WmcProductUnitRecord>(StringComparer.Ordinal);
        foreach (var unit in units)
        {
            var code = NormalizeUnitCode(unit.UnitCode);
            if (code.Length == 0 || !result.TryAdd(code, unit))
            {
                result.Clear();
                return false;
            }
        }
        return true;
    }

    private static IReadOnlyList<WmcProductUnitRecord> ReadUnits(WmcProductRecord row) =>
        row.UnitsSnapshotComplete && row.Units is not null
            ? row.Units
            : throw new InvalidOperationException("A consulta PRODUTOS_UNCONV não confirmou o snapshot completo de unidades.");

    private static IReadOnlyList<WmcProductUnitRecord> ReadAndValidateUnits(WmcProductRecord row)
    {
        var units = ReadUnits(row);
        if (!TryToSourceUnitDictionary(units, out _))
            throw new InvalidOperationException("Unidades WMC vazias ou duplicadas para o mesmo produto.");
        return units;
    }

    private static WmcProductUnitImportProposal BuildUnitProposal(
        Product? product,
        IReadOnlyCollection<WmcProductUnit> current,
        WmcProductRecord sourceRow,
        bool snapshotComplete)
    {
        var source = sourceRow.Units ?? [];
        var validation = WmcManualProductUnitValidation.Validate(
            sourceRow,
            source,
            snapshotComplete);
        var currentPrimary = validation.PrimaryUnitCode is { } primaryCode
            ? current.FirstOrDefault(unit => NormalizeUnitCode(unit.UnitCode) == primaryCode)
            : null;
        var currentMaster = validation.MasterUnitCode is { } masterCode
            ? current.FirstOrDefault(unit => NormalizeUnitCode(unit.UnitCode) == masterCode)
            : null;
        return new(
            validation.PrimaryUnitCode,
            validation.MasterUnitCode,
            product?.Ean,
            validation.ProposedEan,
            product?.Ean13,
            validation.ProposedEan13,
            product?.Dun14,
            validation.ProposedDun14,
            currentPrimary?.Factor,
            validation.PrimaryUnit?.Factor is > 0 ? validation.PrimaryUnit.Factor : null,
            currentMaster?.Factor,
            validation.MasterUnit?.Factor is > 0 ? validation.MasterUnit.Factor : null,
            product?.WmcPackageQuantity,
            validation.ProposedPackageQuantity is > 0 ? validation.ProposedPackageQuantity : null,
            product?.WmcConversionQuantity,
            validation.ProposedConversionQuantity is > 0 ? validation.ProposedConversionQuantity : null,
            validation.IsValid,
            validation.BlockReason);
    }

    private static void ApplyOfficialUnitFields(Product product, WmcProductUnitImportProposal proposal)
    {
        if (proposal.ProposedEan is { } ean) product.Ean = ean;
        if (proposal.ProposedEan13 is { } ean13) product.Ean13 = ean13;
        if (proposal.ProposedDun14 is { } dun14) product.Dun14 = dun14;
    }

    private static bool IsValidUnitBarcode(string? value) => WmcManualProductUnitValidation.IsValidBarcode(value);

    private static void ApplyMappedFields(Product product, WmcProductRecord row, WmcProductUnitImportProposal unitProposal)
    {
        if (!string.IsNullOrWhiteSpace(row.Produto)) product.Name = row.Produto.Trim();
        if (Clean(row.CodMarca) is { } brandCode) product.WmcBrandCode = brandCode;
        if (Clean(row.Marca) is { } brand) product.Brand = brand;
        if (unitProposal.SafeToApply)
        {
            if (Clean(row.Un) is { } unit) product.Unit = unit;
            if (Clean(row.UnAltern) is { } alt) product.WmcAlternateUnit = alt;
            if (row.QtdeEmbalagem is > 0) product.WmcPackageQuantity = row.QtdeEmbalagem;
            if (row.QtdeConversao is > 0) product.WmcConversionQuantity = row.QtdeConversao;
        }
        var situation = WmcProductRules.ClassifySituation(row.Situacao);
        if (situation == WmcProductSituation.Active) product.IsWmcActive = true;
        else if (situation == WmcProductSituation.Inactive) product.IsWmcActive = false;
        else if (!string.IsNullOrWhiteSpace(row.Situacao)) product.IsWmcActive = false;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeUnitCode(string value) => (Clean(value) ?? "").ToUpperInvariant();
    private static string Summarize(IEnumerable<WmcProductUnit> units) => string.Join("; ", units.OrderBy(unit => unit.UnitCode, StringComparer.Ordinal).Select(unit => FormatUnit(unit.UnitCode, unit.Description, unit.Factor, unit.Barcode, unit.IsMaster)));
    private static string Summarize(IEnumerable<WmcProductUnitRecord> units) => string.Join("; ", units.OrderBy(unit => unit.UnitCode, StringComparer.Ordinal).Select(unit => FormatUnit(unit.UnitCode, unit.Description, unit.Factor, unit.Barcode, unit.IsMaster)));
    private static string FormatUnit(string code, string? description, decimal? factor, string? barcode, bool isMaster) => $"{NormalizeUnitCode(code)} ({Clean(description) ?? "sem descrição"}) × {factor?.ToString() ?? "sem fator"} · {Clean(barcode) ?? "sem código"}{(isMaster ? " · master" : "")}";
    private static string PlaceholderSku(string code) => ("WMC-" + code).Substring(0, Math.Min(30, ("WMC-" + code).Length));
}
