using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Areas.Admin.ViewComponents;

public sealed class WmcActivationDiagnosticsViewComponent(WmcActivationDiagnosticsQuery diagnosticsQuery) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var rawOrderId = HttpContext.Request.Query["diagnosticOrderId"].ToString();
        int? orderId = int.TryParse(rawOrderId, out var parsedOrderId) ? parsedOrderId : null;
        var diagnostics = await diagnosticsQuery.ReadAsync(orderId, HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(rawOrderId) && orderId is null)
            diagnostics = diagnostics with { DiagnosticInputError = "Informe um Order.Id numérico positivo." };

        return View("~/Areas/Admin/Views/Shared/Components/WmcActivationDiagnostics/Default.cshtml", diagnostics);
    }
}
