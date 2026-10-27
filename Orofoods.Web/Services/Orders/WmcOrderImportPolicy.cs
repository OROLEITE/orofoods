using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Orders;

public static class WmcOrderImportPolicy
{
    public static bool CanImport(Order order, WmcJsonOrderOptions options)
    {
        if (!options.Enabled
            || !options.ManualImportEnabled
            || order.Status != OrderStatus.Approved
            || order.IntegrationStatus is not (IntegrationStatus.Pending or IntegrationStatus.Failed))
        {
            return false;
        }

        if (order.WmcOrderExportState is { } state
            && (state.RequiresManualReview
                || state.Status is WmcOrderExportStatus.Processing or WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported))
        {
            return false;
        }

        return !order.WmcExportAudits.Any(audit => audit.Outcome is
            WmcExportOutcome.Started or WmcExportOutcome.Unknown or WmcExportOutcome.Succeeded
                or WmcExportOutcome.Imported or WmcExportOutcome.Generated
                or WmcExportOutcome.Available or WmcExportOutcome.Regenerated);
    }
}
