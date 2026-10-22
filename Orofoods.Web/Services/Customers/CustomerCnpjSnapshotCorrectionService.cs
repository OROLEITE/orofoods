using System.Data;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Services.Customers;

public sealed record CustomerCnpjSnapshotCorrectionResult(bool Succeeded, string? ErrorField = null, string? ErrorMessage = null);

public sealed class CustomerCnpjSnapshotCorrectionService(ApplicationDbContext db)
{
    public async Task<CustomerCnpjSnapshotCorrectionResult> CorrectAsync(
        int orderId,
        string? cnpj,
        string? reason,
        string? changedByUserId,
        string? changedByEmail,
        CancellationToken cancellationToken = default)
    {
        if (!CnpjValidator.TryNormalize(cnpj, out var normalizedCnpj))
        {
            return new(false, "Cnpj", "Informe um CNPJ válido, com 14 posições e dígitos verificadores corretos.");
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 1000 || string.IsNullOrWhiteSpace(changedByUserId))
        {
            return new(false, "Reason", "Informe uma justificativa e confirme sua identidade de Administrador.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new(false, null, "Pedido não encontrado.");
        }

        var exportState = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        var exportAudits = await db.WmcExportAudits.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .ToListAsync(cancellationToken);
        if (!CanCorrectSnapshot(order.CustomerCnpjSnapshot, exportState, exportAudits))
        {
            return new(false, "Cnpj", "O snapshot não pode ser corrigido enquanto houver exportação WMC confirmada ou histórico sem confirmação segura.");
        }

        var formattedCnpj = CnpjValidator.Format(normalizedCnpj);
        if (string.Equals(order.CustomerCnpjSnapshot, formattedCnpj, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true);
        }

        db.CustomerCnpjAudits.Add(new CustomerCnpjAudit
        {
            CustomerId = order.CustomerId,
            OrderId = order.Id,
            Action = CustomerCnpjAuditAction.OrderSnapshotCorrected,
            PreviousCnpj = order.CustomerCnpjSnapshot,
            NewCnpj = formattedCnpj,
            Reason = normalizedReason,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = changedByUserId,
            ChangedByEmail = changedByEmail
        });
        order.CustomerCnpjSnapshot = formattedCnpj;

        if (exportState is null)
        {
            db.WmcOrderExportStates.Add(new WmcOrderExportState
            {
                OrderId = orderId,
                Status = WmcOrderExportStatus.Pending,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            exportState.Version++;
            exportState.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Cnpj", "O estado WMC mudou durante a correção. Atualize o pedido e tente novamente.");
        }
        catch (DbUpdateException) when (exportState is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            if (await db.WmcOrderExportStates.AsNoTracking().AnyAsync(x => x.OrderId == orderId, cancellationToken))
            {
                return new(false, "Cnpj", "Uma ação WMC iniciou para este pedido. Atualize o pedido antes de tentar corrigir o snapshot.");
            }

            throw;
        }
    }

    public static bool CanCorrectSnapshot(
        string? snapshot,
        WmcOrderExportState? state,
        IReadOnlyCollection<WmcExportAudit> audits)
    {
        if (!string.IsNullOrWhiteSpace(snapshot) && CnpjValidator.TryNormalize(snapshot, out _))
        {
            return false;
        }

        if (state?.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported
            or WmcOrderExportStatus.Processing or WmcOrderExportStatus.Rejected
            || state?.RequiresManualReview == true)
        {
            return false;
        }

        if (audits.Any(audit => audit.Succeeded
            || audit.Outcome is not WmcExportOutcome.Failed and not WmcExportOutcome.Unknown))
        {
            return false;
        }

        if (state is null && audits.Count > 0)
        {
            return false;
        }

        return state is null || state.Status is WmcOrderExportStatus.Pending or WmcOrderExportStatus.Error;
    }
}
