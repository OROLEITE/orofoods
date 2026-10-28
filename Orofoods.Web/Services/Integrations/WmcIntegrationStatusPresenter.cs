using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Integrations;

public enum WmcIntegrationStatusDisplay
{
    Pending,
    Sent,
    Confirmed,
    Error
}

public static class WmcIntegrationStatusPresenter
{
    public static WmcIntegrationStatusDisplay GetStatus(Order order)
    {
        var state = order.WmcOrderExportState;
        if (state?.RequiresManualReview == true)
        {
            return WmcIntegrationStatusDisplay.Pending;
        }

        if (state?.Status == WmcOrderExportStatus.Imported && !state.RequiresManualReview)
        {
            return WmcIntegrationStatusDisplay.Confirmed;
        }

        if (state?.Status is WmcOrderExportStatus.Rejected or WmcOrderExportStatus.Error)
        {
            return WmcIntegrationStatusDisplay.Error;
        }

        var latestHttpAttempt = order.WmcExportAudits
            .Where(audit => !IsTxtAudit(audit))
            .OrderByDescending(audit => audit.ExportedAt)
            .ThenByDescending(audit => audit.Id)
            .FirstOrDefault();

        if (latestHttpAttempt is not null)
        {
            return latestHttpAttempt.Outcome switch
            {
                WmcExportOutcome.Succeeded or WmcExportOutcome.Imported => WmcIntegrationStatusDisplay.Confirmed,
                WmcExportOutcome.Started or WmcExportOutcome.Unknown => WmcIntegrationStatusDisplay.Sent,
                WmcExportOutcome.Failed or WmcExportOutcome.Rejected => WmcIntegrationStatusDisplay.Error,
                _ => WmcIntegrationStatusDisplay.Pending
            };
        }

        if (order.IntegrationStatus == IntegrationStatus.Failed)
        {
            return WmcIntegrationStatusDisplay.Error;
        }

        if (order.IntegrationStatus == IntegrationStatus.Unknown)
        {
            return WmcIntegrationStatusDisplay.Sent;
        }

        return WmcIntegrationStatusDisplay.Pending;
    }

    public static string GetLabel(WmcIntegrationStatusDisplay status) => status switch
    {
        WmcIntegrationStatusDisplay.Sent => "Enviado",
        WmcIntegrationStatusDisplay.Confirmed => "Confirmado",
        WmcIntegrationStatusDisplay.Error => "Erro",
        _ => "Pendente"
    };

    public static string GetCssClass(WmcIntegrationStatusDisplay status) => status switch
    {
        WmcIntegrationStatusDisplay.Sent => "erp-status-badge--processing",
        WmcIntegrationStatusDisplay.Confirmed => "erp-status-badge--succeeded",
        WmcIntegrationStatusDisplay.Error => "erp-status-badge--failed",
        _ => "erp-status-badge--pending"
    };

    public static string? GetDetail(Order order)
    {
        var status = GetStatus(order);
        if (status == WmcIntegrationStatusDisplay.Pending && order.WmcOrderExportState?.RequiresManualReview == true)
        {
            return "Revisão manual necessária; confira o histórico antes de qualquer novo envio.";
        }

        if (status == WmcIntegrationStatusDisplay.Pending &&
            (order.WmcOrderExportState?.Status == WmcOrderExportStatus.Exported || order.WmcExportAudits.Any(IsTxtAudit)))
        {
            return "TXT gerado não confirma a importação no WMC; integração pendente.";
        }

        if (status == WmcIntegrationStatusDisplay.Sent)
        {
            return "Tentativa HTTP/JSON sem confirmação definitiva. Reconcilie antes de qualquer reenvio.";
        }

        if (status == WmcIntegrationStatusDisplay.Confirmed && string.IsNullOrWhiteSpace(order.ErpOrderNumber))
        {
            return "O WMC confirmou a importação; o número do pedido ainda não foi reconciliado.";
        }

        if (status == WmcIntegrationStatusDisplay.Error)
        {
            var httpError = order.WmcExportAudits
                .Where(audit => !IsTxtAudit(audit))
                .OrderByDescending(audit => audit.ExportedAt)
                .ThenByDescending(audit => audit.Id)
                .Select(audit => audit.Error)
                .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
            return httpError ?? order.IntegrationError ?? order.WmcOrderExportState?.LastError;
        }

        return null;
    }

    private static bool IsTxtAudit(WmcExportAudit audit) =>
        !string.IsNullOrWhiteSpace(audit.FileName) ||
        audit.Outcome is WmcExportOutcome.Generated or WmcExportOutcome.Available or WmcExportOutcome.Regenerated;
}
