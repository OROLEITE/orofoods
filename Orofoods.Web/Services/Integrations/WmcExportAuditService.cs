using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Services.Integrations;

public sealed class WmcExportAuditService(ApplicationDbContext db)
{
    public async Task RecordAsync(
        int orderId,
        string? exportedByUserId,
        string? exportedByEmail,
        string? fileName,
        bool succeeded,
        string? error,
        CancellationToken cancellationToken = default)
    {
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = orderId,
            ExportedByUserId = exportedByUserId,
            ExportedByEmail = exportedByEmail,
            FileName = fileName,
            Succeeded = succeeded,
            Source = WmcExportSource.Manual,
            Outcome = succeeded ? WmcExportOutcome.Succeeded : WmcExportOutcome.Failed,
            Error = error
        });

        await db.SaveChangesAsync(cancellationToken);
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

    public async Task CompleteAttemptAsync(
        int orderId,
        Guid attemptId,
        WmcExportOutcome outcome,
        int? httpStatusCode,
        string? responseBody,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(
            audit => audit.OrderId == orderId && audit.AttemptId == attemptId,
            cancellationToken);
        attempt.Outcome = outcome;
        attempt.Succeeded = outcome == WmcExportOutcome.Succeeded;
        attempt.HttpStatusCode = httpStatusCode;
        attempt.ResponseBody = Limit(responseBody, 2000);
        attempt.Error = Limit(error, 2000);
        attempt.ExportedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTransmissionIdAsync(
        int orderId,
        Guid attemptId,
        int transmissionId,
        CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(
            audit => audit.OrderId == orderId && audit.AttemptId == attemptId,
            cancellationToken);
        attempt.WmcTransmissionId = transmissionId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTransmissionContextAsync(
        int orderId,
        Guid attemptId,
        int transmissionId,
        int salesRepresentativeCode,
        string destination,
        CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(
            audit => audit.OrderId == orderId && audit.AttemptId == attemptId,
            cancellationToken);
        attempt.WmcTransmissionId = transmissionId;
        attempt.WmcSalesRepresentativeCode = salesRepresentativeCode;
        attempt.WmcDestination = destination;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WmcOrderNumberReconciliationTarget?> GetOrderNumberReconciliationTargetAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Where(item => item.Id == orderId)
            .Select(item => new { item.ErpOrderNumber, item.WmcTransmissionId })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null ||
            int.TryParse(order.ErpOrderNumber, out var existingNumber) && existingNumber > 0 ||
            order.WmcTransmissionId is not int orderTransmissionId ||
            orderTransmissionId <= 0)
        {
            return null;
        }

        var audit = await db.WmcExportAudits
            .AsNoTracking()
            .Where(item => item.OrderId == orderId &&
                item.Outcome == WmcExportOutcome.Succeeded &&
                item.Succeeded &&
                item.HttpStatusCode == 200 &&
                item.AttemptId != null &&
                item.WmcTransmissionId == orderTransmissionId &&
                item.WmcSalesRepresentativeCode > 0 &&
                item.WmcSalesRepresentativeCode <= short.MaxValue &&
                item.WmcDestination != null &&
                item.ResponseBody != null)
            .OrderByDescending(item => item.ExportedAt)
            .Select(item => new WmcOrderNumberReconciliationTarget(
                item.AttemptId!.Value,
                item.WmcTransmissionId!.Value,
                item.WmcSalesRepresentativeCode!.Value,
                item.WmcDestination!,
                item.ResponseBody!))
            .FirstOrDefaultAsync(cancellationToken);

        return audit;
    }

    public async Task RecordNroPedidoLookupAsync(
        int orderId,
        Guid attemptId,
        int? httpStatusCode,
        string? responseBody,
        string? error,
        string? erpOrderNumber,
        CancellationToken cancellationToken = default)
    {
        var attempt = await db.WmcExportAudits.SingleAsync(
            audit => audit.OrderId == orderId && audit.AttemptId == attemptId,
            cancellationToken);
        attempt.NroPedidoHttpStatusCode = httpStatusCode;
        attempt.NroPedidoResponseBody = Limit(responseBody, 2000);
        attempt.NroPedidoError = Limit(error, 2000);
        if (erpOrderNumber is not null)
        {
            var order = await db.Orders.SingleAsync(item => item.Id == orderId, cancellationToken);
            order.ErpOrderNumber = erpOrderNumber;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Limit(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record WmcOrderNumberReconciliationTarget(
    Guid AttemptId,
    int WmcTransmissionId,
    int SalesRepresentativeCode,
    string Destination,
    string ExportResponseBody);
