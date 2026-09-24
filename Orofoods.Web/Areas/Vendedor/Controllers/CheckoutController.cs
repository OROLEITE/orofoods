using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Authorization;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Vendedor.Controllers;

[Area("Vendedor")]
[Authorize(Policy = OrofoodsPolicies.LinkedSalesRepresentative)]
public sealed class CheckoutController(
    ApplicationDbContext db,
    SellerCheckoutService checkoutService,
    SalesRepresentativeAccessService accessService,
    OrderPlacementService orderPlacementService,
    OrderReservationService reservationService,
    CartService cartService,
    PaymentOrchestrationService paymentOrchestrationService) : Controller
{
    [HttpGet("/Vendedor/Clientes/{customerId:int}/Checkout")]
    public async Task<IActionResult> Index(int customerId, CancellationToken cancellationToken)
    {
        var checkout = await checkoutService.GetAsync(User, customerId, HttpContext.Session, cancellationToken);
        if (checkout is null) return Forbid();
        if (checkout.Cart.Items.Count == 0) return RedirectToAction("Index", "Cart", new { area = "Vendedor", customerId });
        return View(checkout);
    }

    [HttpPost("/Vendedor/Clientes/{customerId:int}/Checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(int customerId, SellerCheckoutViewModel input, CancellationToken cancellationToken)
    {
        var checkout = await checkoutService.GetAsync(User, customerId, HttpContext.Session, cancellationToken);
        if (checkout is null) return Forbid();
        var attemptMarker = GetAttemptMarker(customerId, input.AttemptKey);
        if (int.TryParse(HttpContext.Session.GetString(attemptMarker), out var existingOrderId))
        {
            return RedirectToAction(nameof(Success), new { customerId, id = existingOrderId });
        }
        if (!ModelState.IsValid || checkout.Cart.Items.Count == 0)
        {
            checkout.ErrorMessage = checkout.Cart.Items.Count == 0 ? "Adicione ao menos um produto." : null;
            return View(checkout);
        }

        if (!checkout.Addresses.Any(x => x.Id == input.AddressId) || !checkout.PaymentTerms.Any(x => x.Id == input.PaymentTermId))
        {
            checkout.ErrorMessage = "Endereço ou condição de pagamento inválidos.";
            return View(checkout);
        }

        var eligibility = await checkoutService.GetAsync(User, customerId, HttpContext.Session, cancellationToken);
        var selectedTerm = eligibility!.PaymentTerms.Single(x => x.Id == input.PaymentTermId);
        if (selectedTerm.Code == "CREDIT_CARD" && (string.IsNullOrWhiteSpace(input.CardToken) || string.IsNullOrWhiteSpace(input.CardPaymentMethodId)))
        {
            checkout.ErrorMessage = "Não foi possível validar os dados do cartão. Tente novamente.";
            return View(checkout);
        }
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        var scope = await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken);
        if (scope is null) return Forbid();

        var placement = await orderPlacementService.PlaceAsync(
            customerId,
            userId,
            new OrderPlacementCommand(input.AddressId, input.PaymentTermId, input.RequestedDeliveryDate, input.Notes),
            HttpContext.Session,
            scope,
            clearCart: false,
            cancellationToken);
        if (!placement.Succeeded)
        {
            checkout.ErrorMessage = placement.Errors.FirstOrDefault();
            return View(checkout);
        }
        HttpContext.Session.SetString(attemptMarker, placement.Order!.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Payment? payment = null;
        if (selectedTerm.Code is "PIX" or "CREDIT_CARD")
        {
            try
            {
                payment = selectedTerm.Code == "PIX"
                    ? await paymentOrchestrationService.CreatePixAsync(placement.Order!.Id, customerId, input.AttemptKey, cancellationToken)
                    : await paymentOrchestrationService.CreateCreditCardAsync(placement.Order!.Id, customerId, input.AttemptKey, input.CardToken ?? "", input.CardPaymentMethodId ?? "", input.CardInstallments, cancellationToken);

                if (payment.Status is PaymentStatus.Rejected or PaymentStatus.Failed)
                {
                    await CancelOrderAsync(placement.Order!, userId, cancellationToken);
                    HttpContext.Session.Remove(attemptMarker);
                    checkout.ErrorMessage = "O pagamento não foi aprovado. Tente novamente.";
                    return View(checkout);
                }
            }
            catch (InvalidOperationException exception)
            {
                await CancelOrderAsync(placement.Order!, userId, cancellationToken);
                HttpContext.Session.Remove(attemptMarker);
                checkout.ErrorMessage = exception.Message;
                return View(checkout);
            }
        }

        cartService.Clear(HttpContext.Session, scope);
        return RedirectToAction(nameof(Success), new { customerId, id = placement.Order!.Id, paymentId = payment?.Id });
    }

    [HttpGet("/Vendedor/Clientes/{customerId:int}/Checkout/Sucesso/{id:int}")]
    public async Task<IActionResult> Success(int customerId, int id, int? paymentId, CancellationToken cancellationToken)
    {
        if (await accessService.GetSellerCartScopeAsync(User, customerId, cancellationToken) is null) return Forbid();
        var order = await db.Orders.AsNoTracking().Include(x => x.Customer).Include(x => x.PaymentTerm)
            .SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId, cancellationToken);
        return order is null ? NotFound() : View(new SellerOrderSuccessViewModel(order.Id, order.Number, order.Customer!.TradeName, order.Total, order.PaymentMethod, order.Status.ToString()));
    }

    private async Task CancelOrderAsync(Order order, string userId, CancellationToken cancellationToken)
    {
        await reservationService.ReleaseAsync(order, cancellationToken);
        order.Status = OrderStatus.Cancelled;
        order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Cancelled, ChangedAt = DateTime.UtcNow, ChangedByUserId = userId });
        await db.SaveChangesAsync(cancellationToken);
    }

    private string GetAttemptMarker(int customerId, string attemptKey) =>
        $"seller-checkout:{User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}:{customerId}:{attemptKey}";
}