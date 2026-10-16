using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;

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
}
