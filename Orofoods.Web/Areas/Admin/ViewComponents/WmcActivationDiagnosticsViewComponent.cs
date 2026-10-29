using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Areas.Admin.ViewComponents;

public sealed class WmcActivationDiagnosticsViewComponent(WmcActivationDiagnosticsQuery diagnosticsQuery) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var diagnostics = await diagnosticsQuery.ReadAsync(HttpContext.RequestAborted);
        return View("~/Areas/Admin/Views/Shared/Components/WmcActivationDiagnostics/Default.cshtml", diagnostics);
    }
}
