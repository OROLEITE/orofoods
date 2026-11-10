using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Orofoods.Web.Services.Integrations;

public sealed class WmcExportAuditService(ApplicationDbContext db)
{
    public async Task RecordAttemptAsync(WmcExportAudit attempt, CancellationToken cancellationToken = default)
    {
        if (attempt.AttemptId is null || attempt.AttemptId == Guid.Empty)
        {
            throw new ArgumentException("Uma chave de tentativa WMC é obrigatória.", nameof(attempt));
        }

        attempt.ExportedAt = DateTime.UtcNow;
        attempt.Error = SummarizeError(attempt.Error,
            attempt.Outcome is WmcExportOutcome.Rejected or WmcExportOutcome.Regenerated ? 1000 : 500);
        attempt.Succeeded = attempt.Outcome is WmcExportOutcome.Generated or WmcExportOutcome.Available
            or WmcExportOutcome.Regenerated or WmcExportOutcome.Imported;

        var existing = await db.WmcExportAudits.SingleOrDefaultAsync(
            audit => audit.OrderId == attempt.OrderId && audit.AttemptId == attempt.AttemptId,
            cancellationToken);
        if (existing is null)
        {
            db.WmcExportAudits.Add(attempt);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                db.Entry(attempt).State = EntityState.Detached;
                existing = await db.WmcExportAudits.SingleOrDefaultAsync(
                    audit => audit.OrderId == attempt.OrderId && audit.AttemptId == attempt.AttemptId,
                    cancellationToken);
                if (existing is null)
                {
                    throw;
                }
            }
        }

        existing.Source = attempt.Source;
        existing.Outcome = attempt.Outcome;
        existing.ExportedByUserId = attempt.ExportedByUserId;
        existing.ExportedByEmail = attempt.ExportedByEmail;
        existing.ExportedAt = attempt.ExportedAt;
        existing.GeneratedAt ??= attempt.GeneratedAt;
        existing.FileName = attempt.FileName;
        existing.Succeeded = attempt.Succeeded;
        existing.Error = attempt.Error;
        existing.HttpStatusCode = attempt.HttpStatusCode;
        existing.ResponseBody = attempt.ResponseBody;
        existing.WmcTransmissionId = attempt.WmcTransmissionId;
        existing.WmcSalesRepresentativeCode = attempt.WmcSalesRepresentativeCode;
        existing.WmcDestination = attempt.WmcDestination;
        existing.NroPedidoHttpStatusCode = attempt.NroPedidoHttpStatusCode;
        existing.NroPedidoResponseBody = attempt.NroPedidoResponseBody;
        existing.NroPedidoError = attempt.NroPedidoError;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordAsync(
        int orderId,
        string? exportedByUserId,
        string? exportedByEmail,
        string? fileName,
        bool succeeded,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = orderId,
            AttemptId = Guid.NewGuid(),
            Source = WmcExportSource.Manual,
            Outcome = succeeded ? WmcExportOutcome.Generated : WmcExportOutcome.Failed,
            ExportedByUserId = exportedByUserId,
            ExportedByEmail = exportedByEmail,
            FileName = fileName,
            Succeeded = succeeded,
            Error = error,
            GeneratedAt = succeeded ? now : null
        }, cancellationToken);
    }

    private static string? SummarizeError(string? error, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var summary = Regex.Replace(error, @"\s+", " ").Trim();
        return summary.Length <= maxLength ? summary : summary[..(maxLength - 3)] + "...";
    }

    public async Task BeginAttemptAsync(WmcExportAudit attempt, CancellationToken cancellationToken = default)
    {
        if (attempt.AttemptId is null || attempt.AttemptId == Guid.Empty)
        {
            throw new ArgumentException("Uma chave de tentativa WMC é obrigatória.", nameof(attempt));
        }

        db.WmcExportAudits.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAttemptAsync(int orderId, Guid attemptId, WmcExportOutcome outcome, int? httpStatusCode,
        string? responseBody, string? error, CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(audit => audit.OrderId == orderId && audit.AttemptId == attemptId, cancellationToken);
        attempt.Outcome = outcome;
        attempt.Succeeded = outcome == WmcExportOutcome.Succeeded;
        attempt.HttpStatusCode = httpStatusCode;
        attempt.ResponseBody = Limit(responseBody, 2000);
        attempt.Error = Limit(error, 2000);
        attempt.ExportedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTransmissionIdAsync(int orderId, Guid attemptId, int transmissionId, CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(audit => audit.OrderId == orderId && audit.AttemptId == attemptId, cancellationToken);
        attempt.WmcTransmissionId = transmissionId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTransmissionContextAsync(int orderId, Guid attemptId, int transmissionId, int salesRepresentativeCode,
        string destination, CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(audit => audit.OrderId == orderId && audit.AttemptId == attemptId, cancellationToken);
        attempt.WmcTransmissionId = transmissionId;
        attempt.WmcSalesRepresentativeCode = salesRepresentativeCode;
        attempt.WmcDestination = destination;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WmcOrderNumberReconciliationTarget?> GetOrderNumberReconciliationTargetAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking().Where(item => item.Id == orderId)
            .Select(item => new { item.ErpOrderNumber, item.WmcTransmissionId }).SingleOrDefaultAsync(cancellationToken);
        if (order is null || int.TryParse(order.ErpOrderNumber, out var existingNumber) && existingNumber > 0 ||
            order.WmcTransmissionId is not int orderTransmissionId || orderTransmissionId <= 0)
        {
            return null;
        }

        return await db.WmcExportAudits.AsNoTracking()
            .Where(item => item.OrderId == orderId &&
                ((item.Outcome == WmcExportOutcome.Succeeded && item.Succeeded && item.HttpStatusCode == 200) ||
                    item.Outcome == WmcExportOutcome.Unknown) &&
                item.AttemptId != null && item.WmcTransmissionId == orderTransmissionId &&
                item.WmcSalesRepresentativeCode > 0 && item.WmcSalesRepresentativeCode <= short.MaxValue &&
                item.WmcDestination != null && (item.Outcome == WmcExportOutcome.Unknown || item.ResponseBody != null))
            .OrderByDescending(item => item.ExportedAt)
            .Select(item => new WmcOrderNumberReconciliationTarget(item.AttemptId!.Value, item.WmcTransmissionId!.Value,
                item.WmcSalesRepresentativeCode!.Value, item.WmcDestination!, item.ResponseBody, item.Outcome))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RecordNroPedidoLookupAsync(int orderId, Guid attemptId, int? httpStatusCode, string? responseBody,
        string? error, string? erpOrderNumber, CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(audit => audit.OrderId == orderId && audit.AttemptId == attemptId, cancellationToken);
        attempt.NroPedidoHttpStatusCode = httpStatusCode;
        attempt.NroPedidoResponseBody = Limit(responseBody, 2000);
        attempt.NroPedidoError = Limit(error, 2000);
        if (erpOrderNumber is not null)
        {
            var order = await db.Orders.SingleAsync(item => item.Id == orderId, cancellationToken);
            order.ErpOrderNumber = erpOrderNumber;
            if (attempt.Outcome == WmcExportOutcome.Unknown)
            {
                order.IntegrationStatus = IntegrationStatus.Succeeded;
                order.IntegrationError = null;
                var state = await db.WmcOrderExportStates.SingleOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
                if (state is null)
                {
                    state = new WmcOrderExportState { OrderId = orderId };
                    db.WmcOrderExportStates.Add(state);
                }

                state.Status = WmcOrderExportStatus.Imported;
                state.RequiresManualReview = false;
                state.UpdatedAt = DateTime.UtcNow;
                state.ImportedAt = DateTime.UtcNow;
                state.ImportedByUserId = attempt.ExportedByUserId;
                state.ImportedByEmail = attempt.ExportedByEmail;
                state.LastError = null;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Limit(string? value, int maxLength) => value is null || value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record WmcOrderNumberReconciliationTarget(Guid AttemptId, int WmcTransmissionId, int SalesRepresentativeCode,
    string Destination, string? ExportResponseBody, WmcExportOutcome Outcome);
