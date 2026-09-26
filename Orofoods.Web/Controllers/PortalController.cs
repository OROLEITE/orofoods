using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Authorization;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Controllers;

[Authorize(Policy = OrofoodsPolicies.ApprovedCustomer)]
public class PortalController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    CustomerAccessService customerAccessService,
    AdminCustomerContextService adminCustomerContextService,
    PriceService priceService,
    CartService cartService,
    CustomerDashboardService customerDashboardService,
    SavedOrderService savedOrderService,
    OrderReservationService orderReservationService,
    IPaymentEligibilityService paymentEligibilityService,
    PaymentOrchestrationService paymentOrchestrationService,
    IOptions<MercadoPagoOptions> mercadoPagoOptions,
    AssistedOrderService assistedOrderService) : Controller
{
    private static readonly TimeZoneInfo BrazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.ActionDescriptor.RouteValues.TryGetValue("action", out var action);
        if (User.IsInRole("Administrador") && !string.Equals(action, nameof(SelectCustomer), StringComparison.Ordinal))
        {
            var customerId = adminCustomerContextService.GetSelectedCustomerId(HttpContext.Session);
            var customer = customerId is null ? null : await customerAccessService.GetApprovedCustomerByIdAsync(customerId.Value);
            if (customer is null)
            {
                adminCustomerContextService.Clear(HttpContext.Session);
                context.Result = RedirectToAction(nameof(SelectCustomer), new { returnUrl = Request.Path + Request.QueryString });
                return;
            }
        }

        await next();
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public async Task<IActionResult> SelectCustomer(string? returnUrl)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action(nameof(Dashboard));
        return View(await db.Customers
            .Where(customer => customer.IsActive && customer.Status == CustomerStatus.Approved)
            .OrderBy(customer => customer.TradeName)
            .ToListAsync());
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectCustomer(int customerId, string? returnUrl)
    {
        if (await customerAccessService.GetApprovedCustomerByIdAsync(customerId) is null)
        {
            ModelState.AddModelError(string.Empty, "Selecione um cliente ativo e aprovado.");
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action(nameof(Dashboard));
            return View(await db.Customers.Where(customer => customer.IsActive && customer.Status == CustomerStatus.Approved).OrderBy(customer => customer.TradeName).ToListAsync());
        }

        adminCustomerContextService.SetSelectedCustomerId(HttpContext.Session, customerId);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Dashboard))!);
    }

    public async Task<IActionResult> Dashboard()
    {
        var customer = await GetCurrentCustomerAsync();
        return View(await customerDashboardService.GetAsync(customer));
    }

    public async Task<IActionResult> Catalog(string? q, string? category, string? brand, bool? available)
    {
        var customer = await GetCurrentCustomerAsync();
        var query = db.Products
            .Include(x => x.ProductCategory)
            .Include(x => x.Images)
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Name.Contains(q) ||
                x.Sku.Contains(q) ||
                x.Brand.Contains(q) ||
                x.Description.Contains(q) ||
                x.ProductCategory!.Name.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.ProductCategory!.Name == category);
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            query = query.Where(x => x.Brand == brand);
        }

        var products = await query.OrderBy(x => x.ProductCategory!.SortOrder).ThenBy(x => x.Name).ToListAsync();
        await ApplyCommercialAvailabilityAsync(products);
        if (available is not null)
        {
            products = products.Where(product => product.IsCommerciallyAvailable == available).ToList();
        }

        ViewBag.Prices = await priceService.GetPricesAsync(customer.Id, products.Select(x => x.Id));
        ViewBag.FavoriteProductIds = await db.FavoriteProducts
            .Where(x => x.CustomerId == customer.Id)
            .Select(x => x.ProductId)
            .ToListAsync();
        ViewBag.Brands = products.Select(x => x.Brand).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
        var cart = await cartService.GetAsync(customer.Id, HttpContext.Session);
        ViewBag.CartQuantity = cart.Items.Sum(item => item.Quantity);
        ViewBag.CartProductQuantities = cart.Items.ToDictionary(item => item.ProductId, item => item.Quantity);
        return View(products);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int productId, string? q, string? category)
    {
        var customer = await GetCurrentCustomerAsync();
        var productExists = await db.Products.AnyAsync(x => x.Id == productId && x.IsActive);
        if (!productExists)
        {
            return NotFound();
        }

        var favorite = await db.FavoriteProducts
            .SingleOrDefaultAsync(x => x.CustomerId == customer.Id && x.ProductId == productId);
        if (favorite is null)
        {
            db.FavoriteProducts.Add(new FavoriteProduct { CustomerId = customer.Id, ProductId = productId });
        }
        else
        {
            db.FavoriteProducts.Remove(favorite);
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Catalog), new { q, category });
    }

    public async Task<IActionResult> Favorites()
    {
        var customer = await GetCurrentCustomerAsync();
        var products = await db.Products
            .Include(x => x.ProductCategory)
            .Where(x => x.IsActive && db.FavoriteProducts.Any(favorite =>
                favorite.CustomerId == customer.Id && favorite.ProductId == x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync();

        ViewBag.Prices = await priceService.GetPricesAsync(customer.Id, products.Select(x => x.Id));
        return View(products);
    }

    public async Task<IActionResult> Product(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var product = await db.Products
            .Include(x => x.ProductCategory)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (product is null)
        {
            return NotFound();
        }

        await ApplyCommercialAvailabilityAsync([product]);
        ViewBag.Price = await priceService.GetPriceAsync(customer.Id, product.Id);
        return View(product);
    }

    public async Task<IActionResult> Cart()
    {
        var customer = await GetCurrentCustomerAsync();
        return View(await cartService.GetAsync(customer.Id, HttpContext.Session));
    }

    public async Task<IActionResult> SavedOrders()
    {
        var customer = await GetCurrentCustomerAsync();
        return View(await savedOrderService.GetActiveAsync(customer.Id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCartAsModel(string name)
    {
        var customer = await GetCurrentCustomerAsync();
        var cart = await cartService.GetAsync(customer.Id, HttpContext.Session);
        if (string.IsNullOrWhiteSpace(name) || !cart.Items.Any())
        {
            TempData["ModelError"] = "Informe um nome e adicione itens ao carrinho.";
            return RedirectToAction(nameof(Cart));
        }

        await savedOrderService.SaveAsync(customer.Id, name, cart.Items.Select(x => new SavedOrderLine(x.ProductId, x.Quantity)));
        return RedirectToAction(nameof(SavedOrders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoadSavedOrder(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var items = await savedOrderService.GetLoadableAsync(customer.Id, id);
        if (!items.Any())
        {
            TempData["ModelError"] = "Esse modelo não possui produtos disponíveis no momento.";
            return RedirectToAction(nameof(SavedOrders));
        }

        await cartService.ReplaceAsync(customer.Id, items, HttpContext.Session);
        return RedirectToAction(nameof(Cart));
    }

    public async Task<IActionResult> EditSavedOrder(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var model = await savedOrderService.GetAsync(customer.Id, id);
        if (model is null) return NotFound();
        return View(new SavedOrderEditViewModel { Id = model.Id, Name = model.Name, Items = model.Items.Select(x => new SavedOrderLine(x.ProductId, x.Quantity)).ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSavedOrder(SavedOrderEditViewModel input)
    {
        var customer = await GetCurrentCustomerAsync();
        if (!ModelState.IsValid || !input.Items.Any()) return View(input);
        await savedOrderService.SaveAsync(customer.Id, input.Name, input.Items, input.Id);
        return RedirectToAction(nameof(SavedOrders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateSavedOrder(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        await savedOrderService.DeactivateAsync(customer.Id, id);
        return RedirectToAction(nameof(SavedOrders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        var customer = await GetCurrentCustomerAsync();
        try
        {
            await cartService.AddAsync(customer.Id, productId, quantity, HttpContext.Session);
        }
        catch (InvalidOperationException exception) when (IsCatalogRequest())
        {
            return BadRequest(new { message = exception.Message });
        }

        if (IsCatalogRequest())
        {
            var cart = await cartService.GetAsync(customer.Id, HttpContext.Session);
            return Json(new
            {
                cartQuantity = cart.Items.Sum(item => item.Quantity),
                quantity = cart.Items.Single(item => item.ProductId == productId).Quantity
            });
        }

        return RedirectToAction(nameof(Catalog));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateCart(int productId, int quantity)
    {
        cartService.Update(productId, quantity, HttpContext.Session);
        return RedirectToAction(nameof(Cart));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveFromCart(int productId)
    {
        cartService.Remove(productId, HttpContext.Session);
        return RedirectToAction(nameof(Cart));
    }

    private bool IsCatalogRequest() => string.Equals(
        Request.Headers["X-Requested-With"],
        "XMLHttpRequest",
        StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> Checkout()
    {
        var customer = await GetCurrentCustomerAsync();
        return View(await BuildCheckoutAsync(customer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel input)
    {
        var customer = await GetCurrentCustomerAsync();
        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("Authenticated user id not found.");
        var attemptMarker = GetCheckoutAttemptMarker(userId, customer.Id, input.AttemptKey);
        if (int.TryParse(HttpContext.Session.GetString(attemptMarker), out var existingOrderId))
        {
            var existingOrder = await db.Orders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == existingOrderId && x.CustomerId == customer.Id);
            if (existingOrder is not null)
            {
                if (existingOrder.PaymentMethod == "PIX")
                {
                    var existingPaymentId = await db.Payments.AsNoTracking()
                        .Where(x => x.OrderId == existingOrder.Id)
                        .Select(x => (int?)x.Id)
                        .SingleOrDefaultAsync();
                    if (existingPaymentId is int paymentId)
                    {
                        return RedirectToAction(nameof(Pix), new { id = paymentId });
                    }
                }

                return RedirectToAction(nameof(Success), new { id = existingOrder.Id });
            }

            HttpContext.Session.Remove(attemptMarker);
        }

        var checkout = await BuildCheckoutAsync(customer);
        if (!ModelState.IsValid || !checkout.Cart.Items.Any() || checkout.Cart.RemainingForMinimum > 0)
        {
            return View(checkout);
        }

        if (!checkout.Addresses.Any(x => x.Id == input.AddressId) || !checkout.PaymentTerms.Any(x => x.Id == input.PaymentTermId))
        {
            ModelState.AddModelError(string.Empty, "Endereço ou condição de pagamento inválidos.");
            return View(checkout);
        }

        var eligibility = await paymentEligibilityService.ValidateAsync(customer.Id, input.PaymentTermId);
        if (!eligibility.IsAllowed)
        {
            ModelState.AddModelError(string.Empty, eligibility.ErrorMessage!);
            return View(checkout);
        }

        if (checkout.PaymentTerms.Single(x => x.Id == input.PaymentTermId).DaysUntilDue > 0 && checkout.Cart.Total > customer.CreditLimit - customer.CreditUsed)
        {
            ModelState.AddModelError(string.Empty, "O total ultrapassa o crédito disponível.");
            return View(checkout);
        }

        var selectedPaymentTerm = checkout.PaymentTerms.Single(x => x.Id == input.PaymentTermId);
        if (selectedPaymentTerm.Code == "CREDIT_CARD" && (string.IsNullOrWhiteSpace(input.CardToken) || string.IsNullOrWhiteSpace(input.CardPaymentMethodId)))
        {
            ModelState.AddModelError(string.Empty, "Não foi possível validar os dados do cartão. Tente novamente.");
            return View(checkout);
        }

        var result = await assistedOrderService.PlaceAsync(
            customer.Id,
            userId,
            new OrderPlacementCommand(input.AddressId, input.PaymentTermId, input.RequestedDeliveryDate, input.Notes, input.AttemptKey),
            HttpContext.Session,
            CartScope.CustomerSelfService,
            clearCart: false);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            return View(checkout);
        }

        HttpContext.Session.SetString(attemptMarker, result.Order!.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var order = await db.Orders.Include(x => x.PaymentTerm).SingleAsync(x => x.Id == result.Order!.Id);
        var orderPaymentTerm = order.PaymentTerm ?? throw new InvalidOperationException("Order payment term not found.");

        Payment? payment = null;
        if (orderPaymentTerm.Code is "PIX" or "CREDIT_CARD")
        {
            try
            {
                payment = orderPaymentTerm.Code == "PIX"
                    ? await paymentOrchestrationService.CreatePixAsync(order.Id, customer.Id, input.AttemptKey)
                    : await paymentOrchestrationService.CreateCreditCardAsync(
                        order.Id, customer.Id, input.AttemptKey, input.CardToken!, input.CardPaymentMethodId!, input.CardInstallments);

                if (payment.Status is PaymentStatus.Rejected or PaymentStatus.Failed)
                {
                    await orderReservationService.ReleaseAsync(order);
                    order.Status = OrderStatus.Cancelled;
                    order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Cancelled, ChangedAt = DateTime.UtcNow, ChangedByUserId = userId });
                    await db.SaveChangesAsync();
                    HttpContext.Session.Remove(attemptMarker);
                    ModelState.AddModelError(string.Empty, "O pagamento não foi aprovado. Tente novamente ou selecione outra condição de pagamento.");
                    return View(checkout);
                }
            }
            catch (InvalidOperationException ex)
            {
                await orderReservationService.ReleaseAsync(order);
                order.Status = OrderStatus.Cancelled;
                order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Cancelled, ChangedAt = DateTime.UtcNow, ChangedByUserId = userId });
                await db.SaveChangesAsync();
                HttpContext.Session.Remove(attemptMarker);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(checkout);
            }
        }

        if (!result.WasIdempotentReplay)
        {
            cartService.Clear(HttpContext.Session);
        }
        if (orderPaymentTerm.Code == "PIX" && payment is not null)
        {
            return RedirectToAction(nameof(Pix), new { id = payment.Id });
        }

        return RedirectToAction(nameof(Success), new { id = order.Id });
    }

    private static string GetCheckoutAttemptMarker(string userId, int customerId, string attemptKey) =>
        $"portal-checkout:{userId}:{customerId}:{attemptKey}";

    private async Task<CheckoutViewModel> BuildCheckoutAsync(Customer customer)
    {
        var eligibility = await paymentEligibilityService.GetAvailablePaymentOptionsAsync(customer.Id);
        return new CheckoutViewModel
        {
            Cart = await cartService.GetAsync(customer.Id, HttpContext.Session),
            Addresses = customer.Addresses.Where(x => x.IsActive).ToList(),
            PaymentTerms = eligibility.PaymentMethods,
            PaymentEligibility = eligibility,
            MercadoPagoPublicKey = mercadoPagoOptions.Value.PublicKey
        };
    }

    public async Task<IActionResult> Repeat(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var order = await db.Orders
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleAsync(x => x.Id == id && x.CustomerId == customer.Id);
        var productIds = order.Items.Select(x => x.ProductId).ToList();
        var prices = await priceService.GetPricesAsync(customer.Id, productIds);
        var products = await db.Products
            .Include(x => x.SubstituteProduct)
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var items = order.Items.Select(item =>
        {
            var product = products[item.ProductId];
            return new RepeatItemVm(
                product.Id,
                product.Sku,
                product.Name,
                product.UnitDescription,
                product.MinimumCases,
                product.IsAvailable ? item.Quantity : 0,
                prices.GetValueOrDefault(product.Id, product.PromotionalPrice ?? product.BasePrice),
                product.IsAvailable,
                product.SubstituteProduct?.Name);
        }).ToList();

        return View(new RepeatOrderVm
        {
            Customer = customer,
            SourceOrder = order,
            Items = items,
            Addresses = customer.Addresses.Where(x => x.IsActive).ToList(),
            PaymentTerms = (await paymentEligibilityService.GetAvailablePaymentOptionsAsync(customer.Id)).PaymentMethods.ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ConfirmOrderVm input)
    {
        var customer = await GetCurrentCustomerAsync();
        var hasDeliveryAddress = await db.CustomerAddresses.AnyAsync(address =>
            address.Id == input.AddressId && address.CustomerId == customer.Id && address.IsActive);
        if (!hasDeliveryAddress)
        {
            return Forbid();
        }

        var isCustomerOrder = await db.Orders.AnyAsync(order =>
            order.Id == input.SourceOrderId && order.CustomerId == customer.Id);
        if (!isCustomerOrder)
        {
            return Forbid();
        }

        var eligibility = await paymentEligibilityService.ValidateAsync(customer.Id, input.PaymentTermId);
        if (!eligibility.IsAllowed)
        {
            return RedirectToAction(nameof(Repeat), new { id = input.SourceOrderId, error = eligibility.ErrorMessage });
        }

        var paymentTerm = await db.PaymentTerms.SingleOrDefaultAsync(term => term.Id == input.PaymentTermId && term.IsActive);
        if (paymentTerm is null) return Forbid();

        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("Authenticated user id not found.");
        var products = await db.Products
            .Where(x => input.ProductIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var prices = await priceService.GetPricesAsync(customer.Id, input.ProductIds);
        var order = new Order
        {
            CustomerId = customer.Id,
            CreatedByUserId = userId,
            DeliveryAddressId = input.AddressId,
            RequestedDeliveryDate = input.RequestedDate,
            Status = OrderStatus.Received,
            PaymentTermId = paymentTerm.Id,
            PaymentMethod = paymentTerm.Name,
            Notes = input.Notes,
            CreatedAt = DateTime.UtcNow
        };
        order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Received, ChangedAt = order.CreatedAt, ChangedByUserId = userId });

        for (var i = 0; i < Math.Min(input.ProductIds.Length, input.Quantities.Length); i++)
        {
            if (!products.TryGetValue(input.ProductIds[i], out var product) || !product.IsAvailable || input.Quantities[i] <= 0)
            {
                continue;
            }

            var quantity = Math.Max(input.Quantities[i], product.MinimumCases);
            var unitPrice = prices.GetValueOrDefault(product.Id, product.PromotionalPrice ?? product.BasePrice);
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = unitPrice,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = product.Sku,
                Subtotal = quantity * unitPrice
            });
        }

        order.Subtotal = order.Items.Sum(x => x.Subtotal);
        order.Total = order.Subtotal + order.Freight;

        var error = !order.Items.Any()
            ? "Nenhum item deste pedido está disponível no momento. Escolha produtos disponíveis no catálogo."
            : order.Total < customer.MinimumOrder
            ? $"O pedido mínimo é {customer.MinimumOrder:C}."
            : paymentTerm.DaysUntilDue > 0 && order.Total > customer.CreditLimit - customer.CreditUsed
                ? "O total ultrapassa o credito disponivel."
            : null;

        if (error is not null)
        {
            return RedirectToAction(nameof(Repeat), new { id = input.SourceOrderId, error });
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var reservation = await orderReservationService.ReserveAsync(order);
        if (!reservation.IsValid)
        {
            db.Orders.Remove(order);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Repeat), new { id = input.SourceOrderId, error = reservation.ErrorMessage });
        }

        order.Number = $"ORO-{DateTime.Now:yyyy}-{order.Id:000000}";
        order.ConfirmedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Success), new { id = order.Id });
    }

    public async Task<IActionResult> Success(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var order = await db.Orders
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.DeliveryAddress)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == id && x.CustomerId == customer.Id);
        return View(order);
    }

    public async Task<IActionResult> Pix(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id);
        return payment is null ? NotFound() : View(PixPaymentViewModel.FromPayment(payment, BrazilTimeZone));
    }

    public async Task<IActionResult> Orders(string? q, string? status)
    {
        var customer = await GetCurrentCustomerAsync();
        var query = db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.PaymentTerm)
            .Where(x => x.CustomerId == customer.Id);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Number.Contains(q));
        if (Enum.TryParse<OrderStatus>(status, true, out var parsedStatus)) query = query.Where(x => x.Status == parsedStatus);
        return View(new OrderHistoryViewModel { Orders = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(), Query = q, Status = status });
    }

    public async Task<IActionResult> Order(int id)
    {
        var customer = await GetCurrentCustomerAsync();
        var order = await db.Orders.Include(x => x.Items).Include(x => x.DeliveryAddress).Include(x => x.PaymentTerm).Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id);
        return order is null ? NotFound() : View(order);
    }

    private async Task<Customer> GetCurrentCustomerAsync()
    {
        Customer? customer = null;
        if (User.IsInRole("Administrador"))
        {
            var customerId = adminCustomerContextService.GetSelectedCustomerId(HttpContext.Session);
            if (customerId is not null)
            {
                var selectedCustomer = await customerAccessService.GetApprovedCustomerByIdAsync(customerId.Value);
                if (selectedCustomer is not null)
                {
                    customer = selectedCustomer;
                }
            }
        }

        if (customer is null)
        {
            var userId = userManager.GetUserId(User);
            customer = await customerAccessService.GetApprovedCustomerAsync(userId)
                ?? throw new InvalidOperationException("No approved customer is linked to the current user.");
        }

        ViewData["PortalCustomerName"] = customer.TradeName;
        return customer;
    }

    private async Task ApplyCommercialAvailabilityAsync(IEnumerable<Product> products)
    {
        var productList = products.ToList();
        var productIds = productList.Select(product => product.Id).ToList();
        var inventories = await db.ProductInventories.AsNoTracking()
            .Where(inventory => productIds.Contains(inventory.ProductId))
            .ToDictionaryAsync(inventory => inventory.ProductId);
        foreach (var product in productList)
        {
            product.IsCommerciallyAvailable = product.IsAvailable && inventories.TryGetValue(product.Id, out var inventory) && inventory.AvailableQuantity >= product.MinimumCases;
        }
    }
}
