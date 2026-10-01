using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Orofoods.Web.Api;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class OrdersController(
    ApplicationDbContext db,
    AdminOrderService service,
    OrderIntegrationService integrationService,
    IPointPaymentOrchestrationService pointPaymentService,
    IOptions<MercadoPagoPointOptions> pointOptions,
    IOptions<PaymentEligibilityOptions> paymentOptions,
    IHostEnvironment hostEnvironment) : Controller
{
    public async Task<IActionResult> Index(string? q, string? status, string sort = "date", string direction = "desc", int page = 1)
    {
        var query = db.Orders.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Number.Contains(q) || x.Customer!.TradeName.Contains(q));
        if (Enum.TryParse<OrderStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        var descending = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        query = (sort.ToLowerInvariant(), descending) switch
        {
            ("number", false) => query.OrderBy(x => x.Number),
            ("number", true) => query.OrderByDescending(x => x.Number),
            ("customer", false) => query.OrderBy(x => x.Customer!.TradeName),
            ("customer", true) => query.OrderByDescending(x => x.Customer!.TradeName),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("total", false) => query.OrderBy(x => x.Total),
            ("total", true) => query.OrderByDescending(x => x.Total),
            ("date", false) => query.OrderBy(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };

        var totalItems = await query.CountAsync();
        var request = new PageRequest(page, 25);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)request.PageSize));
        if (request.Page > totalPages) request = new PageRequest(totalPages, request.PageSize);
        var orders = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync();

        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Direction = descending ? "desc" : "asc";
        ViewBag.PagedResult = new PagedResult<Order>(orders, request.Page, request.PageSize, totalItems);
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).Include(x => x.DeliveryAddress).Include(x => x.PaymentTerm)
            .Include(x => x.Payments).ThenInclude(x => x!.DriverPaymentTerminalAssignment).ThenInclude(x => x!.Driver)
            .Include(x => x.Payments).ThenInclude(x => x!.DriverPaymentTerminalAssignment).ThenInclude(x => x!.PaymentTerminal)
            .Include(x => x.StatusHistory).ThenInclude(x => x.ChangedByUser).Include(x => x.WmcExportAudits).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id);
        if (order is null) return NotFound();
        var pointEnabled = IsPointEnabled() && order.PaymentTerm?.Code == "CARD_ON_DELIVERY";
        ViewBag.PointPaymentEnabled = pointEnabled;
        ViewBag.PointEligibleAssignments = pointEnabled
            ? await pointPaymentService.GetEligibleAssignmentsAsync()
            : [];
        var activeCardPayment = order.Payments.Where(payment => payment.Method == Orofoods.Web.Models.Payments.PaymentMethodType.CardOnDelivery
                && payment.Status is Orofoods.Web.Models.Payments.PaymentStatus.Pending or Orofoods.Web.Models.Payments.PaymentStatus.Processing or Orofoods.Web.Models.Payments.PaymentStatus.ActionRequired)
            .OrderByDescending(payment => payment.CreatedAt).ThenByDescending(payment => payment.Id).FirstOrDefault();
        var pointAssignments = ViewBag.PointEligibleAssignments as IReadOnlyList<Orofoods.Web.Models.Payments.DriverPaymentTerminalAssignment> ?? [];
        ViewBag.PointChargeBlockReason = order.Status is OrderStatus.Delivered or OrderStatus.Cancelled
            ? "Este pedido não aceita uma nova cobrança."
            : order.Total <= 0m
                ? "O valor do pedido não permite cobrança."
                : order.Payments.Any(payment => payment.Method == Orofoods.Web.Models.Payments.PaymentMethodType.CardOnDelivery
                    && payment.Status is Orofoods.Web.Models.Payments.PaymentStatus.Approved or Orofoods.Web.Models.Payments.PaymentStatus.Paid)
                    ? "O pagamento deste pedido já foi aprovado."
                    : activeCardPayment?.Gateway is not null and not "MercadoPagoPoint"
                        ? "Há uma tentativa ativa vinculada a outro meio de pagamento."
                        : activeCardPayment?.Gateway == "MercadoPagoPoint"
                            ? null
                            : pointAssignments.Count == 0
                                ? "Nenhum motorista ativo com o terminal virtual SBX0000001 associado. Cadastre ou reative o vínculo antes de cobrar."
                                : null;
        ViewBag.PointAuditEvents = cardOnDeliveryPayments(order).Count == 0
            ? []
            : await db.PointPaymentAuditEvents.AsNoTracking().Where(x => x.OrderId == order.Id)
                .Include(x => x.AdminUser).OrderByDescending(x => x.OccurredAt).Take(20).ToListAsync();
        ViewBag.PointRequestKey = Guid.NewGuid().ToString("N");
        return View(order);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPointCharge(int id, int assignmentId, string requestKey, CancellationToken cancellationToken = default)
    {
        if (!IsPointEnabled())
        {
            TempData["PointPaymentError"] = "A cobrança Cartão na Entrega está desabilitada neste ambiente.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var result = await pointPaymentService.StartChargeAsync(id, assignmentId, requestKey, cancellationToken, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (result.Succeeded) TempData["PointPaymentMessage"] = "Cobrança enviada ao terminal virtual. O status será atualizado automaticamente.";
        else TempData["PointPaymentError"] = result.ErrorMessage ?? "Não foi possível iniciar a cobrança.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RefreshPointCharge(int id, int paymentId, CancellationToken cancellationToken = default) =>
        ReconcilePointCharge(id, paymentId, cancel: false, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> CancelPointCharge(int id, int paymentId, CancellationToken cancellationToken = default) =>
        ReconcilePointCharge(id, paymentId, cancel: true, cancellationToken);

    private async Task<IActionResult> ReconcilePointCharge(int orderId, int paymentId, bool cancel, CancellationToken cancellationToken)
    {
        if (!IsPointEnabled())
        {
            TempData["PointPaymentError"] = "A cobrança Cartão na Entrega está desabilitada neste ambiente.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
        var belongsToOrder = await db.Payments.AsNoTracking().AnyAsync(payment => payment.Id == paymentId
            && payment.OrderId == orderId && payment.Method == Orofoods.Web.Models.Payments.PaymentMethodType.CardOnDelivery
                    && payment.Gateway == "MercadoPagoPoint", cancellationToken);
        if (!belongsToOrder)
        {
            TempData["PointPaymentError"] = "A cobrança não pertence a este pedido.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
        var result = cancel
            ? await pointPaymentService.CancelAsync(paymentId, cancellationToken, User.FindFirstValue(ClaimTypes.NameIdentifier))
            : await pointPaymentService.RefreshAsync(paymentId, cancellationToken, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (result.Succeeded) TempData["PointPaymentMessage"] = cancel ? "Solicitação de cancelamento enviada; status atualizado." : "Status da cobrança atualizado.";
        else TempData["PointPaymentError"] = result.ErrorMessage ?? "Não foi possível atualizar a cobrança.";
        return RedirectToAction(nameof(Details), new { id = orderId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PollPointCharge(int id, int paymentId, CancellationToken cancellationToken = default)
    {
        if (!IsPointEnabled()) return StatusCode(StatusCodes.Status409Conflict, new { message = "A atualização automática está indisponível neste ambiente." });
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == paymentId && x.OrderId == id
            && x.Method == Orofoods.Web.Models.Payments.PaymentMethodType.CardOnDelivery && x.Gateway == "MercadoPagoPoint", cancellationToken);
        if (payment is null) return NotFound(new { message = "Tentativa de cobrança não encontrada." });

        var result = await pointPaymentService.RefreshAsync(paymentId, cancellationToken, User.FindFirstValue(ClaimTypes.NameIdentifier));
        var current = result.Payment ?? payment;
        var terminal = current.Status is Orofoods.Web.Models.Payments.PaymentStatus.Approved
            or Orofoods.Web.Models.Payments.PaymentStatus.Paid
            or Orofoods.Web.Models.Payments.PaymentStatus.Rejected
            or Orofoods.Web.Models.Payments.PaymentStatus.Cancelled
            or Orofoods.Web.Models.Payments.PaymentStatus.Expired
            or Orofoods.Web.Models.Payments.PaymentStatus.Refunded
            or Orofoods.Web.Models.Payments.PaymentStatus.Failed;
        return Json(new
        {
            succeeded = result.Succeeded,
            status = current.Status.ToString(),
            label = PointStatusLabel(current.Status),
            terminal,
            message = result.Succeeded ? null : result.ErrorMessage
        });
    }

    private static string PointStatusLabel(Orofoods.Web.Models.Payments.PaymentStatus status) => status switch
    {
        Orofoods.Web.Models.Payments.PaymentStatus.Pending => "Aguardando pagamento",
        Orofoods.Web.Models.Payments.PaymentStatus.Processing => "Aguardando terminal",
        Orofoods.Web.Models.Payments.PaymentStatus.ActionRequired => "Ação necessária no terminal",
        Orofoods.Web.Models.Payments.PaymentStatus.Approved or Orofoods.Web.Models.Payments.PaymentStatus.Paid => "Pagamento aprovado",
        Orofoods.Web.Models.Payments.PaymentStatus.Rejected => "Pagamento recusado",
        Orofoods.Web.Models.Payments.PaymentStatus.Cancelled => "Pagamento cancelado",
        Orofoods.Web.Models.Payments.PaymentStatus.Refunded => "Pagamento estornado",
        Orofoods.Web.Models.Payments.PaymentStatus.Expired => "Cobrança expirada",
        Orofoods.Web.Models.Payments.PaymentStatus.Failed => "Erro de comunicação",
        _ => "Status de pagamento indisponível"
    };

    private static IReadOnlyList<Orofoods.Web.Models.Payments.Payment> cardOnDeliveryPayments(Order order) => order.Payments
        .Where(payment => payment.Method == Orofoods.Web.Models.Payments.PaymentMethodType.CardOnDelivery).ToList();

    private bool IsPointEnabled() => pointOptions.Value.Enabled
        && paymentOptions.Value.CardOnDeliveryEnabled
        && hostEnvironment.IsEnvironment("Test")
        && string.Equals(pointOptions.Value.Environment, "Test", StringComparison.OrdinalIgnoreCase);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var result = await service.UpdateStatusAsync(id, status, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!result.Succeeded) TempData["OrderStatusError"] = result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprocessIntegration(int id)
    {
        await integrationService.SendAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }
}
