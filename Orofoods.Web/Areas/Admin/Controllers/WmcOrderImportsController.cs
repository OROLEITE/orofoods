using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize]
public sealed class WmcOrderImportsController(
    ApplicationDbContext db,
    WmcOrderImportAccessService accessService,
    OrderIntegrationService orderIntegrationService,
    WmcJsonOrderErpIntegration jsonIntegration,
    IOptions<WmcJsonOrderOptions> jsonOptions) : Controller
{
    [HttpGet, Authorize(Roles = "Administrador,Vendedor,Operador")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var ordersQuery = db.Orders.AsNoTracking()
            .Where(order => order.Status == OrderStatus.Approved || order.Status == OrderStatus.Received)
            .Include(order => order.Customer)
            .Include(order => order.WmcOrderExportState)
            .Include(order => order.WmcExportAudits);
        var scopedOrders = await accessService.ApplyOrderScopeAsync(User, ordersQuery, cancellationToken);
        if (scopedOrders is null)
        {
            return Forbid();
        }

        var orders = await scopedOrders
            .OrderByDescending(order => order.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        ViewData["WmcImportEligibleOrderIds"] = orders
            .Where(CanImport)
            .Select(order => order.Id)
            .ToHashSet();
        ViewData["WmcImportEnabled"] = jsonOptions.Value.Enabled && jsonOptions.Value.ManualImportEnabled;
        return View(orders);
    }

    [HttpPost, Authorize(Roles = "Administrador,Vendedor,Operador"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportToWmc(int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(item => item.Customer)
            .Include(item => item.WmcOrderExportState)
            .Include(item => item.WmcExportAudits)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (!await accessService.CanAccessCustomerAsync(User, order.CustomerId, cancellationToken))
        {
            return Forbid();
        }

        if (!CanImport(order))
        {
            TempData["WmcImportError"] = "Este pedido não está elegível para importação ou já possui uma tentativa que exige conferência.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Forbid();
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var result = await orderIntegrationService.SendSelectedAsync(
            order.Id,
            jsonIntegration,
            userId,
            userEmail,
            cancellationToken);
        if (result is null)
        {
            TempData["WmcImportError"] = "O pedido deixou de estar elegível ou outra tentativa já o reservou. Confira o histórico antes de agir.";
        }
        else if (result.Succeeded && !string.IsNullOrWhiteSpace(result.ErpOrderNumber))
        {
            TempData["WmcImportMessage"] = $"Pedido {order.Number} importado no WMC. Número retornado: {result.ErpOrderNumber}.";
        }
        else if (result.Succeeded)
        {
            TempData["WmcImportMessage"] = $"Pedido {order.Number} confirmado pelo WMC. O número do pedido ainda não foi retornado por nropedido.php; não reenvie.";
        }
        else if (result.OutcomeUnknown)
        {
            TempData["WmcImportError"] = $"Resultado incerto para {order.Number}. O reenvio está bloqueado; confira o histórico no WMC.";
        }
        else
        {
            TempData["WmcImportError"] = $"O WMC não confirmou {order.Number}: {result.Error ?? "falha antes da transmissão"}. A tentativa pode ser revisada antes de novo envio.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Administrador,Vendedor,Operador"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReconcileNroPedido(int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(item => item.WmcOrderExportState)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (!await accessService.CanAccessCustomerAsync(User, order.CustomerId, cancellationToken))
        {
            return Forbid();
        }

        if (order.WmcOrderExportState?.Status != WmcOrderExportStatus.Imported
            || !string.IsNullOrWhiteSpace(order.ErpOrderNumber))
        {
            TempData["WmcImportError"] = "Somente pedidos importados e sem número WMC podem consultar nropedido.php.";
            return RedirectToAction(nameof(Index));
        }

        var result = await jsonIntegration.ReconcileNroPedidoAsync(order.Id, cancellationToken);
        if (result.Recovered && !string.IsNullOrWhiteSpace(result.ErpOrderNumber))
        {
            TempData["WmcImportMessage"] = $"Número WMC {result.ErpOrderNumber} reconciliado para o pedido {order.Number}.";
        }
        else
        {
            TempData["WmcImportError"] = $"A importação do pedido {order.Number} continua confirmada, mas nropedido.php ainda não retornou um número. Nenhum novo exportar.php foi enviado.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool CanImport(Order order) => WmcOrderImportPolicy.CanImport(order, jsonOptions.Value);
}
