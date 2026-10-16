using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public enum WmcInitialLoadReleaseStatus { Released, AlreadyReady, Disabled, NotFound, Blocked }
public sealed record WmcInitialLoadReleaseResult(WmcInitialLoadReleaseStatus Status, string Message);

public sealed class WmcInitialLoadReadinessReleaseService(
    ApplicationDbContext db,
    IWebHostEnvironment environment,
    IOptions<WmcInitialLoadReleaseOptions> options,
    TimeProvider timeProvider)
{
    public async Task<WmcInitialLoadReleaseResult> ReleaseAsync(int productId, string? reason, string? adminUserId, CancellationToken cancellationToken = default)
    {
        if (!environment.IsStaging() || !options.Value.Enabled)
            return new(WmcInitialLoadReleaseStatus.Disabled, "A liberação manual está desativada neste ambiente.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 300 || string.IsNullOrWhiteSpace(adminUserId))
            return new(WmcInitialLoadReleaseStatus.Blocked, "Informe um motivo de até 300 caracteres e um usuário administrador autenticado.");

        var product = await db.Products.AsNoTracking()
            .Where(x => x.Id == productId && x.WmcCode == "243680")
            .Select(x => new { x.Id, x.WmcCode, x.WmcInitialLoadReady, x.IsWmcActive, x.WmcStockAvailable })
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null)
            return new(WmcInitialLoadReleaseStatus.NotFound, "A liberação está restrita ao produto WMC 243680.");
        if (product.WmcInitialLoadReady)
            return new(WmcInitialLoadReleaseStatus.AlreadyReady, "A carga inicial WMC já está liberada para este produto.");
        if (!product.IsWmcActive)
            return new(WmcInitialLoadReleaseStatus.Blocked, "O produto está inativo no WMC.");
        if (!product.WmcStockAvailable)
            return new(WmcInitialLoadReleaseStatus.Blocked, "O indicador de estoque WMC está indisponível.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var changed = await db.Products
            .Where(x => x.Id == productId && x.WmcCode == "243680" && !x.WmcInitialLoadReady
                && x.IsWmcActive && x.WmcStockAvailable && x.IsActive && x.IsAvailable
                && db.ProductInventories.Any(i => i.ProductId == x.Id && i.IsManuallyAdjusted
                    && i.QuantityOnHand - i.QuantityReserved >= (x.CommercialUnitCode == "PCT" ? 1 : x.MinimumCases)))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.WmcInitialLoadReady, true), cancellationToken);

        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var current = await db.Products.AsNoTracking()
                .Where(x => x.Id == productId && x.WmcCode == "243680")
                .Select(x => new { x.WmcInitialLoadReady, x.IsWmcActive, x.WmcStockAvailable })
                .SingleOrDefaultAsync(cancellationToken);
            if (current is null)
                return new(WmcInitialLoadReleaseStatus.NotFound, "A liberação está restrita ao produto WMC 243680.");
            if (current.WmcInitialLoadReady)
                return new(WmcInitialLoadReleaseStatus.AlreadyReady, "A carga inicial WMC já está liberada para este produto.");
            if (!current.IsWmcActive)
                return new(WmcInitialLoadReleaseStatus.Blocked, "O produto está inativo no WMC.");
            if (!current.WmcStockAvailable)
                return new(WmcInitialLoadReleaseStatus.Blocked, "O indicador de estoque WMC está indisponível.");
            return new(WmcInitialLoadReleaseStatus.Blocked, "Produto, ajuste manual ou saldo disponível não atende às condições de liberação.");
        }

        db.WmcInitialLoadReleaseAudits.Add(new WmcInitialLoadReleaseAudit
        {
            ProductId = product.Id, WmcCode = product.WmcCode!, AdminUserId = adminUserId,
            ReleasedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(), Reason = reason.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(WmcInitialLoadReleaseStatus.Released, "Liberação manual registrada com auditoria.");
    }

    public async Task<WmcInitialLoadReleaseResult> ActivateBimboAsync(
        int productId,
        string? reason,
        string? adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsStaging() || !options.Value.Enabled)
            return new(WmcInitialLoadReleaseStatus.Disabled, "A ativação comercial BIMBO está desativada neste ambiente.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 300 || string.IsNullOrWhiteSpace(adminUserId))
            return new(WmcInitialLoadReleaseStatus.Blocked, "Informe um motivo de até 300 caracteres e um administrador autenticado.");

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var product = await db.Products
            .Include(item => item.ProductCategory)
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is null || !WmcBimboProductRules.ApprovedProductCodes.Contains(WmcBimboProductRules.NormalizeCode(product.WmcCode), StringComparer.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(WmcInitialLoadReleaseStatus.NotFound, "Produto fora do lote BIMBO homologado.");
        }

        if (product.IsActive && product.IsAvailable && product.WmcInitialLoadReady)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(WmcInitialLoadReleaseStatus.AlreadyReady, "O produto BIMBO já está ativo no catálogo.");
        }

        var inventory = await db.ProductInventories.SingleOrDefaultAsync(item => item.ProductId == product.Id, cancellationToken);
        var validationIssues = WmcBimboProductRules.ValidateCommercialData(product, product.ProductCategory, inventory);
        if (validationIssues.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(WmcInitialLoadReleaseStatus.Blocked, string.Join(" ", validationIssues.Select(issue => issue.Message)));
        }

        product.IsActive = true;
        product.IsAvailable = true;
        product.WmcInitialLoadReady = true;
        db.WmcInitialLoadReleaseAudits.Add(new WmcInitialLoadReleaseAudit
        {
            ProductId = product.Id,
            WmcCode = product.WmcCode!,
            AdminUserId = adminUserId,
            ReleasedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
            Reason = reason.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(WmcInitialLoadReleaseStatus.Released, "Produto BIMBO ativado após validação comercial e liberação auditada.");
    }
}
