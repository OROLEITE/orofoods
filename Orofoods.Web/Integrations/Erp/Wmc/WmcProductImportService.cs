using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcProductImportService(ApplicationDbContext db, IWmcProductReader reader)
{
    public async Task<IReadOnlyList<WmcProductImportCandidate>> GetCandidatesAsync(short brandCode, CancellationToken ct = default)
    {
        var source = await reader.GetAllByBrandWithUnitsAsync(brandCode, ct);
        var products = await db.Products.AsNoTracking().Where(p => p.WmcCode != null).ToListAsync(ct);
        var local = products.GroupBy(p => WmcProductRules.NormalizeCode(p.WmcCode), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var duplicates = WmcProductRules.FindDuplicateCodes(source).ToHashSet(StringComparer.Ordinal);
        var productIds = products.Select(product => product.Id).ToArray();
        var unitsByProduct = await db.WmcProductUnits.AsNoTracking()
            .Where(unit => productIds.Contains(unit.ProductId))
            .ToListAsync(ct);
        var groupedUnits = unitsByProduct.GroupBy(unit => unit.ProductId).ToDictionary(group => group.Key, group => group.ToArray());

        return source.Select(row =>
        {
            var code = WmcProductRules.NormalizeCode(row.CodProduto);
            local.TryGetValue(code, out var matches);
            var product = matches?.Count == 1 ? matches[0] : null;
            var currentUnits = product is not null && groupedUnits.TryGetValue(product.Id, out var productUnits)
                ? productUnits
                : [];
            var sourceUnits = row.Units ?? [];
            var unitProposal = BuildUnitProposal(product, currentUnits, sourceUnits, row.UnitsSnapshotComplete);
            var changes = product is null
                ? Array.Empty<WmcProductFieldChange>()
                : GetChanges(product, row)
                    .Concat(sourceUnits.Count == 0 ? [] : GetUnitChanges(currentUnits, sourceUnits))
                    .Concat(GetUnitFieldChanges(unitProposal))
                    .ToArray();
            var invalidUnitSnapshot = !row.UnitsSnapshotComplete || row.Units is null || sourceUnits.Count == 0 || HasDuplicateUnitCodes(sourceUnits);
            var duplicate = string.IsNullOrWhiteSpace(code) || duplicates.Contains(code) || (matches?.Count ?? 0) > 1
                || (product is null && string.IsNullOrWhiteSpace(row.Produto)) || invalidUnitSnapshot;
            var changed = changes.Length > 0;
            var invalidSituation = !string.IsNullOrWhiteSpace(row.Situacao) && WmcProductRules.ClassifySituation(row.Situacao) == WmcProductSituation.Unknown;
            var status = duplicate ? "Inválido" : product is null ? "Novo" : changed ? "Com alterações" : "Já cadastrado";
            if (invalidUnitSnapshot) status += " - unidades oficiais nao confirmadas";
            var localCommercialUnit = product is null
                ? null
                : string.IsNullOrWhiteSpace(product.CommercialUnitCode) ? product.Unit : product.CommercialUnitCode;
            if (invalidSituation) status += " · situação inválida; venda bloqueada ao reimportar";
            return new WmcProductImportCandidate(
                code, row.Produto, unitProposal.ProposedEan, row.Situacao, Clean(row.CodMarca), Clean(row.Marca), Clean(row.Un),
                product?.Id, status, !duplicate, changes,
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
                UnitProposal: unitProposal);
        }).ToArray();
    }

    public async Task<WmcProductImportResult> ImportAsync(short brandCode, IReadOnlyCollection<string> selectedCodes, bool reimportExisting, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var codes = selectedCodes.Select(WmcProductRules.NormalizeCode).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var itemResults = new List<WmcProductImportItemResult>();
        var created = 0; var updated = 0; var unchanged = 0;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var rows = await reader.GetAllByBrandWithUnitsAsync(brandCode, ct);
            var dups = WmcProductRules.FindDuplicateCodes(rows).ToHashSet(StringComparer.Ordinal);
            var byCode = rows.Where(r => !string.IsNullOrWhiteSpace(r.CodProduto)).GroupBy(r => WmcProductRules.NormalizeCode(r.CodProduto), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            if (codes.Length == 0 || codes.Length != selectedCodes.Count || codes.Any(code => !byCode.TryGetValue(code, out var found) || found.Length != 1 || dups.Contains(code)))
                throw new InvalidOperationException("A seleção mudou ou contém produto inválido/duplicado. Atualize a prévia.");

            var requestedRows = codes.Select(code => byCode[code][0]).ToArray();
            var unitsByCode = requestedRows.ToDictionary(
                row => WmcProductRules.NormalizeCode(row.CodProduto),
                row =>
                {
                    var units = ReadUnits(row);
                    _ = ToSourceUnitDictionary(units);
                    return units;
                },
                StringComparer.Ordinal);
            var trackedProducts = await db.Products.Where(p => p.WmcCode != null).ToListAsync(ct);
            var localGroups = trackedProducts.GroupBy(p => WmcProductRules.NormalizeCode(p.WmcCode), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
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
                    unchanged++; itemResults.Add(new(code, "Ignorado", "Produto existente; reimportação recusada.")); continue;
                }
                var isNew = product is null;
                if (isNew)
                {
                    if (string.IsNullOrWhiteSpace(row.Produto)) throw new InvalidOperationException($"Nome WMC ausente para novo produto {code}.");
                    product = new Product { WmcCode = code, Sku = PlaceholderSku(code), Name = "", ProductCategoryId = pending.Id, IsActive = false, IsAvailable = false, IsWmcActive = false, WmcStockAvailable = false };
                    db.Products.Add(product); created++;
                }
                var current = product!;
                var officialUnits = unitsByCode[code];
                var productUnits = !isNew && existingUnitsByProduct.TryGetValue(current.Id, out var localUnits)
                    ? localUnits
                    : [];
                var unitProposal = BuildUnitProposal(current, productUnits, officialUnits, snapshotComplete: true);
                var before = GetChanges(current, row).Any()
                    || (!isNew && officialUnits.Count > 0 && !UnitSetsEqual(productUnits, officialUnits))
                    || (!isNew && HasUnitFieldChanges(unitProposal));
                ApplyMappedFields(current, row);
                ApplyOfficialUnitFields(current, unitProposal);
                if (officialUnits.Count > 0)
                    ReconcileUnits(current, productUnits, officialUnits, isNew);
                if (!isNew && before) updated++;
                else if (!isNew) unchanged++;
                itemResults.Add(new(code, isNew ? "Criado" : before ? "Atualizado" : "Sem alterações", null));
                if (matches is null) localGroups[code] = [current];
            }
            var completed = DateTime.UtcNow;
            var result = new WmcProductImportResult(codes.Length, created, updated, unchanged, 0, "Succeeded", null, itemResults, completed);
            db.WmcProductImportAudits.Add(Audit(brandCode, started, result));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await RecordFailureAsync(brandCode, codes.Length, started, ex.Message, ct);
        }

    }

    public async Task<DateTime?> GetLastProductImportAsync(CancellationToken ct = default)
    {
        var importAt = await db.WmcProductImportAudits.AsNoTracking().Where(a => a.Status == "Succeeded")
            .MaxAsync(a => (DateTime?)a.CompletedAt, ct);
        var syncAt = await db.WmcSyncRuns.AsNoTracking().Where(r => r.Status == "Succeeded" && r.ProductsRead > 0)
            .MaxAsync(r => r.FinishedAt, ct);
        return new[] { importAt, syncAt }.Where(value => value.HasValue).Max();
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

    private static IEnumerable<WmcProductFieldChange> GetChanges(Product p, WmcProductRecord r)
    {
        if (!string.IsNullOrWhiteSpace(r.Produto) && p.Name != r.Produto.Trim()) yield return new("Descrição", p.Name, r.Produto.Trim());
        if (Clean(r.CodMarca) is { } brand && p.WmcBrandCode != brand) yield return new("Código da marca", p.WmcBrandCode ?? "", brand);
        if (Clean(r.Marca) is { } brandName && p.Brand != brandName) yield return new("Marca", p.Brand, brandName);
        if (Clean(r.Un) is { } unit && p.Unit != unit) yield return new("Unidade", p.Unit, unit);
        if (Clean(r.UnAltern) is { } alt && p.WmcAlternateUnit != alt) yield return new("Unidade alternativa", p.WmcAlternateUnit ?? "", alt);
        var situation = WmcProductRules.ClassifySituation(r.Situacao);
        if (situation == WmcProductSituation.Active && !p.IsWmcActive) yield return new("Situação WMC", "Inativo", "Ativo");
        else if (situation == WmcProductSituation.Inactive && p.IsWmcActive) yield return new("Situação WMC", "Ativo", "Inativo");
        else if (situation == WmcProductSituation.Unknown && !string.IsNullOrWhiteSpace(r.Situacao) && p.IsWmcActive)
            yield return new("Situação WMC", "Ativo", "Bloqueado: situação inválida");
    }

    private static IEnumerable<WmcProductFieldChange> GetUnitChanges(
        IReadOnlyCollection<WmcProductUnit> current,
        IReadOnlyCollection<WmcProductUnitRecord> source)
    {
        if (!UnitSetsEqual(current, source))
        {
            yield return new("Unidades oficiais WMC", Summarize(current), Summarize(source));
        }
    }

    private static IEnumerable<WmcProductFieldChange> GetUnitFieldChanges(WmcProductUnitImportProposal proposal)
    {
        if (proposal.ProposedEan is { } ean && !string.Equals(proposal.CurrentEan, ean, StringComparison.Ordinal))
            yield return new("EAN oficial da PC", proposal.CurrentEan ?? "", ean);
        if (proposal.ProposedEan13 is { } ean13 && !string.Equals(proposal.CurrentEan13, ean13, StringComparison.Ordinal))
            yield return new("EAN-13 oficial da PC", proposal.CurrentEan13 ?? "", ean13);
        if (proposal.ProposedDun14 is { } dun14 && !string.Equals(proposal.CurrentDun14, dun14, StringComparison.Ordinal))
            yield return new("DUN-14 oficial da CX", proposal.CurrentDun14 ?? "", dun14);
        if (proposal.ProposedPcFactor is decimal pcFactor && proposal.CurrentPcFactor != pcFactor)
            yield return new("Fator oficial da PC", proposal.CurrentPcFactor?.ToString() ?? "", pcFactor.ToString());
        if (proposal.ProposedCxFactor is decimal cxFactor && proposal.CurrentCxFactor != cxFactor)
            yield return new("Fator oficial da CX", proposal.CurrentCxFactor?.ToString() ?? "", cxFactor.ToString());
    }

    private static bool HasUnitFieldChanges(WmcProductUnitImportProposal proposal) =>
        GetUnitFieldChanges(proposal).Any();

    private void ReconcileUnits(
        Product product,
        List<WmcProductUnit> current,
        IReadOnlyList<WmcProductUnitRecord> source,
        bool isNew)
    {
        var sourceByCode = ToSourceUnitDictionary(source);
        var currentByCode = current.ToDictionary(unit => NormalizeUnitCode(unit.UnitCode), StringComparer.Ordinal);

        foreach (var (code, existing) in currentByCode)
        {
            if (!sourceByCode.ContainsKey(code))
            {
                db.WmcProductUnits.Remove(existing);
            }
        }

        foreach (var (code, incoming) in sourceByCode)
        {
            if (currentByCode.TryGetValue(code, out var existing))
            {
                if (Clean(incoming.Description) is { } description)
                    existing.Description = description;
                if (incoming.Factor is > 0)
                    existing.Factor = incoming.Factor;
                if (IsValidUnitBarcode(code, incoming.Barcode))
                    existing.Barcode = Clean(incoming.Barcode);
                existing.IsMaster = incoming.IsMaster;
                continue;
            }

            var unit = new WmcProductUnit
            {
                UnitCode = code,
                Description = Clean(incoming.Description) ?? "",
                Factor = incoming.Factor is > 0 ? incoming.Factor : null,
                Barcode = IsValidUnitBarcode(code, incoming.Barcode) ? Clean(incoming.Barcode) : null,
                IsMaster = incoming.IsMaster
            };
            if (isNew)
            {
                product.WmcUnits.Add(unit);
            }
            else
            {
                unit.ProductId = product.Id;
                db.WmcProductUnits.Add(unit);
            }
        }
    }

    private static bool UnitSetsEqual(
        IEnumerable<WmcProductUnit> current,
        IEnumerable<WmcProductUnitRecord> source)
    {
        var currentByCode = current.ToDictionary(unit => NormalizeUnitCode(unit.UnitCode), StringComparer.Ordinal);
        if (!TryToSourceUnitDictionary(source, out var sourceByCode))
            return false;
        if (currentByCode.Count != sourceByCode.Count)
        {
            return false;
        }

        foreach (var (code, local) in currentByCode)
        {
            if (!sourceByCode.TryGetValue(code, out var official)
                || (Clean(official.Description) is { } description && !string.Equals(local.Description.Trim(), description, StringComparison.Ordinal))
                || (official.Factor is > 0 && local.Factor != official.Factor)
                || (IsValidUnitBarcode(code, official.Barcode) && !string.Equals(Clean(local.Barcode), Clean(official.Barcode), StringComparison.Ordinal))
                || local.IsMaster != official.IsMaster)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryToSourceUnitDictionary(
        IEnumerable<WmcProductUnitRecord> units,
        out Dictionary<string, WmcProductUnitRecord> result)
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

    private static Dictionary<string, WmcProductUnitRecord> ToSourceUnitDictionary(IEnumerable<WmcProductUnitRecord> units)
    {
        var result = new Dictionary<string, WmcProductUnitRecord>(StringComparer.Ordinal);
        foreach (var unit in units)
        {
            var code = NormalizeUnitCode(unit.UnitCode);
            if (code.Length == 0 || !result.TryAdd(code, unit))
            {
                throw new InvalidOperationException("Unidades WMC vazias ou duplicadas para o mesmo produto.");
            }
        }

        return result;
    }

    private static bool HasDuplicateUnitCodes(IEnumerable<WmcProductUnitRecord> units)
    {
        var codes = units.Select(unit => NormalizeUnitCode(unit.UnitCode)).ToArray();
        return codes.Any(string.IsNullOrWhiteSpace) || codes.Length != codes.Distinct(StringComparer.Ordinal).Count();
    }

    private static WmcProductUnitImportProposal BuildUnitProposal(
        Product? product,
        IReadOnlyCollection<WmcProductUnit> current,
        IReadOnlyList<WmcProductUnitRecord> source,
        bool snapshotComplete)
    {
        var reasons = new List<string>();
        var currentPc = current.FirstOrDefault(unit => NormalizeUnitCode(unit.UnitCode) == "PC");
        var currentCx = current.FirstOrDefault(unit => NormalizeUnitCode(unit.UnitCode) == "CX");
        var pcUnits = source.Where(unit => NormalizeUnitCode(unit.UnitCode) == "PC").ToArray();
        var cxUnits = source.Where(unit => NormalizeUnitCode(unit.UnitCode) == "CX").ToArray();

        if (!snapshotComplete)
            reasons.Add("Snapshot de PRODUTOS_UNCONV incompleto.");
        if (HasDuplicateUnitCodes(source))
            reasons.Add("Unidades WMC vazias ou duplicadas.");
        if (pcUnits.Length != 1)
            reasons.Add("A origem não confirmou uma única unidade PC.");
        if (cxUnits.Length != 1 || !cxUnits[0].IsMaster)
            reasons.Add("A origem não confirmou uma única unidade CX master.");

        var pc = pcUnits.Length == 1 ? pcUnits[0] : null;
        var cx = cxUnits.Length == 1 && cxUnits[0].IsMaster ? cxUnits[0] : null;
        var proposedEan = pc is not null && IsValidUnitBarcode("PC", pc.Barcode) ? Clean(pc.Barcode) : null;
        var proposedDun = cx is not null && IsValidUnitBarcode("CX", cx.Barcode) ? Clean(cx.Barcode) : null;
        var proposedPcFactor = pc?.Factor is > 0 ? pc.Factor : null;
        var proposedCxFactor = cx?.Factor is > 0 ? cx.Factor : null;
        if (pc is not null && proposedEan is null)
            reasons.Add("EAN-13 da PC ausente ou inválido.");
        if (cx is not null && proposedDun is null)
            reasons.Add("DUN-14 da CX master ausente ou inválido.");
        if (pc is not null && proposedPcFactor is null)
            reasons.Add("Fator da PC ausente ou inválido.");
        if (cx is not null && proposedCxFactor is null)
            reasons.Add("Fator da CX ausente ou inválido.");

        return new(
            product?.Ean,
            proposedEan,
            product?.Ean13,
            proposedEan,
            product?.Dun14,
            proposedDun,
            currentPc?.Factor,
            proposedPcFactor,
            currentCx?.Factor,
            proposedCxFactor,
            reasons.Count == 0,
            reasons.Count == 0 ? null : string.Join(" ", reasons));
    }

    private static void ApplyOfficialUnitFields(Product product, WmcProductUnitImportProposal proposal)
    {
        if (proposal.ProposedEan is { } ean && proposal.ProposedEan13 is { } ean13)
        {
            product.Ean = ean;
            product.Ean13 = ean13;
        }

        if (proposal.ProposedDun14 is { } dun14)
            product.Dun14 = dun14;
    }

    private static bool IsValidUnitBarcode(string unitCode, string? value) =>
        unitCode switch
        {
            "PC" => IsValidBarcode(value, 13),
            "CX" => IsValidBarcode(value, 14),
            _ => !string.IsNullOrWhiteSpace(value)
        };

    private static bool IsValidBarcode(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var digits = value.Trim();
        if (digits.Length != length || digits.Any(character => character is < '0' or > '9')) return false;
        var sum = 0;
        for (var index = digits.Length - 2; index >= 0; index--)
        {
            var positionFromRight = digits.Length - 1 - index;
            sum += (digits[index] - '0') * (positionFromRight % 2 == 1 ? 3 : 1);
        }

        return (10 - (sum % 10)) % 10 == digits[^1] - '0';
    }

    private static IReadOnlyList<WmcProductUnitRecord> ReadUnits(WmcProductRecord row) =>
        row.UnitsSnapshotComplete && row.Units is not null
            ? row.Units
            : throw new InvalidOperationException("A consulta PRODUTOS_UNCONV não confirmou o snapshot completo de unidades.");

    private static string Summarize(IEnumerable<WmcProductUnit> units) => string.Join("; ", units
        .OrderBy(unit => unit.UnitCode, StringComparer.Ordinal)
        .Select(unit => FormatUnit(unit.UnitCode, unit.Description, unit.Factor, unit.Barcode, unit.IsMaster)));

    private static string Summarize(IEnumerable<WmcProductUnitRecord> units) => string.Join("; ", units
        .OrderBy(unit => unit.UnitCode, StringComparer.Ordinal)
        .Select(unit => FormatUnit(unit.UnitCode, unit.Description, unit.Factor, unit.Barcode, unit.IsMaster)));

    private static string FormatUnit(string code, string? description, decimal? factor, string? barcode, bool isMaster) =>
        $"{NormalizeUnitCode(code)} ({Clean(description) ?? "sem descrição"}) × {factor?.ToString() ?? "sem fator"} · {Clean(barcode) ?? "sem código"}{(isMaster ? " · master" : "")}";

    private static string NormalizeUnitCode(string value) => (Clean(value) ?? "").ToUpperInvariant();

    private static void ApplyMappedFields(Product p, WmcProductRecord r)
    {
        if (!string.IsNullOrWhiteSpace(r.Produto)) p.Name = r.Produto.Trim();
        if (Clean(r.CodMarca) is { } brandCode) p.WmcBrandCode = brandCode;
        if (Clean(r.Marca) is { } brand) p.Brand = brand;
        if (Clean(r.Un) is { } unit) p.Unit = unit;
        if (Clean(r.UnAltern) is { } alt) p.WmcAlternateUnit = alt;
        var situation = WmcProductRules.ClassifySituation(r.Situacao);
        if (situation == WmcProductSituation.Active) p.IsWmcActive = true;
        else if (situation == WmcProductSituation.Inactive) p.IsWmcActive = false;
        else if (!string.IsNullOrWhiteSpace(r.Situacao)) p.IsWmcActive = false;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string PlaceholderSku(string code) => ("WMC-" + code).Substring(0, Math.Min(30, ("WMC-" + code).Length));
}
