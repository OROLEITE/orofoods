using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Orofoods.Web.Models;
using Orofoods.Web.Api;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador,Vendedor,GerenteComercial")]
public class CustomersController(ApplicationDbContext db, CustomerApprovalService customerApprovalService, SalesRepresentativeAccessService accessService, IPaymentEligibilityService paymentEligibilityService, PriceService priceService, AssistedOrderService assistedOrderService, CommercialAttentionService attentionService) : Controller
{
    public async Task<IActionResult> Index(string? q, string? status, string sort = "status", string direction = "asc", int page = 1)
    {
        var scope = await accessService.GetScopeAsync(User);
        var query = accessService.ApplyCustomerScope(db.Customers, scope)
            .Include(x => x.PriceTable)
            .Include(x => x.SalesRepresentative)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(customer => customer.TradeName.Contains(q) || customer.LegalName.Contains(q) || customer.Cnpj.Contains(q) || customer.Email.Contains(q));
        }
        if (Enum.TryParse<CustomerStatus>(status, true, out var parsedStatus)) query = query.Where(customer => customer.Status == parsedStatus);

        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort.ToLowerInvariant(), descending) switch
        {
            ("customer", true) => query.OrderByDescending(customer => customer.TradeName),
            ("customer", false) => query.OrderBy(customer => customer.TradeName),
            ("cnpj", true) => query.OrderByDescending(customer => customer.Cnpj),
            ("cnpj", false) => query.OrderBy(customer => customer.Cnpj),
            ("seller", true) => query.OrderByDescending(customer => customer.SalesRepresentative!.Name),
            ("seller", false) => query.OrderBy(customer => customer.SalesRepresentative!.Name),
            ("status", true) => query.OrderByDescending(customer => customer.Status).ThenBy(customer => customer.TradeName),
            _ => query.OrderBy(customer => customer.Status).ThenBy(customer => customer.TradeName)
        };

        var totalItems = await query.CountAsync();
        var request = new PageRequest(page, 25);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)request.PageSize));
        if (request.Page > totalPages) request = new PageRequest(totalPages, request.PageSize);
        var customers = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync();
        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.Sort = sort;
        ViewBag.Direction = descending ? "desc" : "asc";
        ViewBag.PendingCustomers = await accessService.ApplyCustomerScope(db.Customers, scope).CountAsync(customer => customer.Status == CustomerStatus.Pending);
        ViewBag.PagedResult = new PagedResult<Customer>(customers, request.Page, request.PageSize, totalItems);
        return View(customers);
    }

    [Authorize(Roles = "Administrador,GerenteComercial")]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await db.Customers
            .Include(x => x.CustomerPaymentTerms)
            .SingleAsync(x => x.Id == id);

        return View(await BuildViewModelAsync(customer));
    }

    public async Task<IActionResult> Details(int id)
    {
        var scope = await accessService.GetScopeAsync(User);
        var customer = await accessService.ApplyCustomerScope(db.Customers, scope)
            .AsNoTracking()
            .Include(x => x.PriceTable)
            .Include(x => x.SalesRepresentative)
            .Include(x => x.InternalSalesUser)
            .Include(x => x.Addresses)
            .Include(x => x.CustomerPaymentTerms)
            .ThenInclude(x => x.PaymentTerm)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        var orders = await db.Orders
            .AsNoTracking()
            .Include(x => x.PaymentTerm)
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .ToListAsync();
        var activities = await db.CommercialActivities
            .AsNoTracking()
            .Include(x => x.SalesRepresentative)
            .Include(x => x.AssignedUser)
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.ScheduledAt)
            .Take(12)
            .ToListAsync();

        var purchasedProductRows = await db.OrderItems
            .AsNoTracking()
            .Where(item => item.Order!.CustomerId == id && item.Order.Status != OrderStatus.Cancelled)
            .GroupBy(item => new { item.SkuSnapshot, item.ProductNameSnapshot })
            .Select(group => new
            {
                group.Key.SkuSnapshot,
                group.Key.ProductNameSnapshot,
                TotalQuantity = group.Sum(item => item.Quantity),
                OrderCount = group.Select(item => item.OrderId).Distinct().Count(),
                LastPurchaseAt = group.Max(item => item.Order!.CreatedAt),
                TotalAmount = group.Sum(item => item.Subtotal)
            })
            .OrderByDescending(item => item.TotalQuantity)
            .ThenBy(item => item.ProductNameSnapshot)
            .Take(25)
            .ToListAsync();
        var purchasedProducts = purchasedProductRows
            .Select(item => new CustomerProductPurchaseViewModel(
                item.SkuSnapshot,
                item.ProductNameSnapshot,
                item.TotalQuantity,
                item.OrderCount,
                item.LastPurchaseAt,
                item.TotalAmount,
                item.TotalQuantity == 0 ? 0 : item.TotalAmount / item.TotalQuantity))
            .ToList();

        var payments = await db.Payments
            .AsNoTracking()
            .Where(payment => payment.CustomerId == id)
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(25)
            .ToListAsync();

        var userRoles = await (from user in db.Users.AsNoTracking()
                               where user.CustomerId == id
                               join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId into rolesByUser
                               from userRole in rolesByUser.DefaultIfEmpty()
                               join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id into roles
                               from role in roles.DefaultIfEmpty()
                               select new { user.Id, user.Email, user.PhoneNumber, user.IsActive, RoleName = role.Name })
            .ToListAsync();
        var users = userRoles
            .GroupBy(user => new { user.Id, user.Email, user.PhoneNumber, user.IsActive })
            .Select(group => new CustomerUserViewModel(
                group.Key.Email ?? "Sem e-mail",
                group.Key.PhoneNumber,
                group.Key.IsActive,
                string.Join(", ", group.Select(user => user.RoleName).Where(role => role is not null).OrderBy(role => role))))
            .ToList();

        var priceTableItems = customer.PriceTableId is null
            ? []
            : await db.PriceTableItems.AsNoTracking()
                .Include(item => item.Product)
                .Where(item => item.PriceTableId == customer.PriceTableId)
                .OrderBy(item => item.Product!.Name)
                .Take(50)
                .ToListAsync();

        var completedOrders = orders.Where(order => order.Status != OrderStatus.Cancelled).ToList();
        var timeline = activities.Select(activity => new CustomerTimelineItemViewModel(
                activity.CompletedAt ?? activity.ScheduledAt,
                activity.Title,
                $"{activity.Type} · {activity.Status}",
                "fa-headset"))
            .Concat(orders.Select(order => new CustomerTimelineItemViewModel(
                order.CreatedAt,
                $"Pedido {order.Number}",
                $"{order.Status.ToDisplayName()} · {order.Total:C}",
                "fa-receipt")))
            .OrderByDescending(item => item.OccurredAt)
            .Take(30)
            .ToList();

        return View(new CustomerCommercialViewModel
        {
            Customer = customer,
            Orders = orders,
            Activities = activities,
            PurchasedProducts = purchasedProducts,
            Payments = payments,
            PriceTableItems = priceTableItems,
            Users = users,
            Timeline = timeline,
            TotalPurchased = completedOrders.Sum(order => order.Total),
            LastPurchaseAt = completedOrders.FirstOrDefault()?.CreatedAt,
            NextActivity = activities.Where(x => x.ScheduledAt >= DateTime.Today && x.Status != CommercialActivityStatus.Cancelled && x.Status != CommercialActivityStatus.Completed).OrderBy(x => x.ScheduledAt).FirstOrDefault()
            ,OrderCount = completedOrders.Count
            ,AverageTicket = completedOrders.Count == 0 ? 0 : completedOrders.Average(order => order.Total)
            ,OutstandingAmount = payments.Where(payment => payment.Status is PaymentStatus.Pending or PaymentStatus.Issued or PaymentStatus.Overdue).Sum(payment => payment.Amount)
            ,OverdueAmount = payments.Where(payment => payment.Status == PaymentStatus.Overdue).Sum(payment => payment.Amount)
            ,Attention = (await attentionService.GetAsync(User, DateTime.Now)).Customers.SingleOrDefault(x => x.CustomerId == id)
        });
    }

    [HttpGet]
    public async Task<IActionResult> NewOrder(int id, string? q)
    {
        var scope = await accessService.GetScopeAsync(User);
        var customer = await accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope)
            .Include(x => x.Addresses)
            .Include(x => x.SalesRepresentative)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.Status == CustomerStatus.Approved);
        if (customer is null) return Forbid();

        var productsQuery = db.Products.AsNoTracking().Where(x => x.IsActive && x.IsAvailable);
        if (!string.IsNullOrWhiteSpace(q)) productsQuery = productsQuery.Where(x => x.Name.Contains(q) || x.Sku.Contains(q) || x.Brand.Contains(q));
        var products = await productsQuery.OrderBy(x => x.Name).Take(30).ToListAsync();
        var eligibility = await paymentEligibilityService.GetAvailablePaymentOptionsAsync(id);
        var prices = await priceService.GetPricesAsync(id, products.Select(x => x.Id));
        return View(new AssistedOrderViewModel
        {
            CustomerId = id,
            CustomerName = customer.TradeName,
            SalesRepresentativeName = customer.SalesRepresentative?.Name ?? "Não atribuído",
            Addresses = customer.Addresses.Where(x => x.IsActive).ToList(),
            PaymentTerms = eligibility.PaymentMethods,
            PaymentEligibility = eligibility,
            Products = products.Select(x => new AssistedOrderProductViewModel(x.Id, x.Sku, x.Name, x.UnitDescription, prices.GetValueOrDefault(x.Id, x.PromotionalPrice ?? x.BasePrice), x.MinimumCases, true)).ToList(),
            Query = q
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewOrder(AssistedOrderViewModel input, CancellationToken cancellationToken)
    {
        var scope = await accessService.GetScopeAsync(User);
        if (!await accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).AnyAsync(x => x.Id == input.CustomerId, cancellationToken)) return Forbid();
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();
        var attemptKey = $"CrmAssistedOrder:{input.CustomerId}:{input.AttemptKey}";
        if (HttpContext.Session.GetString(attemptKey) is string existingOrderId && int.TryParse(existingOrderId, out var parsedOrderId))
        {
            return RedirectToAction("Details", "Orders", new { area = "Admin", id = parsedOrderId });
        }
        var lines = input.ProductIds.Zip(input.Quantities, (productId, quantity) => (ProductId: productId, Quantity: quantity)).ToList();
        var result = await assistedOrderService.CreateAsync(input.CustomerId, userId, input.AddressId, input.PaymentTermId, input.RequestedDeliveryDate, input.Notes, lines, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return RedirectToAction(nameof(NewOrder), new { id = input.CustomerId });
        }
        HttpContext.Session.SetString(attemptKey, result.OrderId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return RedirectToAction("Details", "Orders", new { area = "Admin", id = result.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteActivity(int id, int customerId)
    {
        var scope = await accessService.GetScopeAsync(User);
        if (!await accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).AnyAsync(x => x.Id == customerId))
        {
            return Forbid();
        }
        var activity = await db.CommercialActivities.SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);
        if (activity is null)
        {
            return NotFound();
        }

        activity.Status = CommercialActivityStatus.Completed;
        activity.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(AdminCustomerApprovalViewModel input, bool approve = false)
    {
        if (approve)
        {
            input.Status = CustomerStatus.Approved;
        }

        if (!ModelState.IsValid)
        {
            var customer = await db.Customers.Include(x => x.CustomerPaymentTerms).SingleAsync(x => x.Id == input.Id);
            var viewModel = await BuildViewModelAsync(customer);
            viewModel.Status = input.Status;
            viewModel.MinimumOrder = input.MinimumOrder;
            viewModel.CreditLimit = input.CreditLimit;
            viewModel.CreditOverrideEnabled = input.CreditOverrideEnabled;
            viewModel.MaximumPaymentTermDays = input.MaximumPaymentTermDays;
            viewModel.CreditBlocked = input.CreditBlocked;
            viewModel.CreditNotes = input.CreditNotes;
            viewModel.WmcCode = input.WmcCode;
            viewModel.PriceTableId = input.PriceTableId;
            viewModel.SalesRepresentativeId = input.SalesRepresentativeId;
            viewModel.InternalSalesUserId = input.InternalSalesUserId;
            viewModel.PaymentTermIds = input.PaymentTermIds;
            return View(viewModel);
        }

        await customerApprovalService.ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = input.Id,
            Status = input.Status,
            MinimumOrder = input.MinimumOrder,
            CreditLimit = input.CreditLimit,
            CreditOverrideEnabled = input.CreditOverrideEnabled,
            MaximumPaymentTermDays = input.MaximumPaymentTermDays,
            CreditBlocked = input.CreditBlocked,
            CreditNotes = input.CreditNotes,
            WmcCode = input.WmcCode,
            PriceTableId = input.PriceTableId,
            SalesRepresentativeId = input.SalesRepresentativeId,
            InternalSalesUserId = input.InternalSalesUserId,
            PaymentTermIds = input.PaymentTermIds
        });

        TempData["CustomerApprovalSuccess"] = input.Status == CustomerStatus.Approved
            ? "Cliente aprovado e acesso comercial liberado com sucesso."
            : "Configurações comerciais salvas com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminCustomerApprovalViewModel> BuildViewModelAsync(Customer customer)
    {
        var eligibility = await paymentEligibilityService.GetAvailablePaymentOptionsAsync(customer.Id);
        return new AdminCustomerApprovalViewModel
        {
            Id = customer.Id,
            LegalName = customer.LegalName,
            TradeName = customer.TradeName,
            Cnpj = customer.Cnpj,
            Status = customer.Status,
            MinimumOrder = customer.MinimumOrder,
            CreditLimit = customer.CreditLimit,
            ValidPurchaseCount = eligibility.ValidPurchases,
            InvoiceCreditEnabled = eligibility.InvoiceCreditEnabled,
            EffectiveMaximumPaymentTermDays = eligibility.MaximumTermDays,
            EffectiveCreditReleaseDate = customer.CreditReleaseDate,
            CreditOverrideEnabled = customer.CreditOverrideEnabled,
            MaximumPaymentTermDays = customer.MaximumPaymentTermDays,
            CreditBlocked = customer.CreditBlocked,
            CreditNotes = customer.CreditNotes,
            WmcCode = customer.WmcCode,
            PriceTableId = customer.PriceTableId,
            SalesRepresentativeId = customer.SalesRepresentativeId,
            InternalSalesUserId = customer.InternalSalesUserId,
            PaymentTermIds = customer.CustomerPaymentTerms.Select(x => x.PaymentTermId).ToList(),
            PriceTables = await db.PriceTables
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToListAsync(),
            SalesRepresentatives = await db.SalesRepresentatives
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToListAsync(),
            InternalSalesUsers = await db.Users
                .Where(x => x.IsActive && x.CustomerId == null)
                .OrderBy(x => x.Email)
                .Select(x => new SelectListItem(x.Email ?? x.UserName!, x.Id, x.Id == customer.InternalSalesUserId))
                .ToListAsync(),
            PaymentTerms = await db.PaymentTerms
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToListAsync()
        };
    }
}
