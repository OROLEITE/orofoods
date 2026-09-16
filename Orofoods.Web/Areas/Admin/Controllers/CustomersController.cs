using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador,Vendedor")]
public class CustomersController(ApplicationDbContext db, CustomerApprovalService customerApprovalService, SalesRepresentativeAccessService accessService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var scope = await accessService.GetScopeAsync(User);
        var customers = await accessService.ApplyCustomerScope(db.Customers, scope)
            .Include(x => x.PriceTable)
            .Include(x => x.SalesRepresentative)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.TradeName)
            .ToListAsync();
        return View(customers);
    }

    [Authorize(Roles = "Administrador")]
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
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        var orders = await db.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .ToListAsync();
        var activities = await db.CommercialActivities
            .AsNoTracking()
            .Include(x => x.SalesRepresentative)
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.ScheduledAt)
            .Take(12)
            .ToListAsync();

        return View(new CustomerCommercialViewModel
        {
            Customer = customer,
            Orders = orders,
            Activities = activities,
            TotalPurchased = await db.Orders.Where(x => x.CustomerId == id && x.Status != OrderStatus.Cancelled).SumAsync(x => (decimal?)x.Total) ?? 0,
            LastPurchaseAt = orders.FirstOrDefault()?.CreatedAt,
            NextActivity = activities.Where(x => x.ScheduledAt >= DateTime.Today && x.Status != CommercialActivityStatus.Cancelled && x.Status != CommercialActivityStatus.Completed).OrderBy(x => x.ScheduledAt).FirstOrDefault()
        });
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
            viewModel.WmcCode = input.WmcCode;
            viewModel.PriceTableId = input.PriceTableId;
            viewModel.SalesRepresentativeId = input.SalesRepresentativeId;
            viewModel.PaymentTermIds = input.PaymentTermIds;
            return View(viewModel);
        }

        await customerApprovalService.ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = input.Id,
            Status = input.Status,
            MinimumOrder = input.MinimumOrder,
            CreditLimit = input.CreditLimit,
            WmcCode = input.WmcCode,
            PriceTableId = input.PriceTableId,
            SalesRepresentativeId = input.SalesRepresentativeId,
            PaymentTermIds = input.PaymentTermIds
        });

        TempData["CustomerApprovalSuccess"] = input.Status == CustomerStatus.Approved
            ? "Cliente aprovado e acesso comercial liberado com sucesso."
            : "Configurações comerciais salvas com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminCustomerApprovalViewModel> BuildViewModelAsync(Customer customer)
    {
        return new AdminCustomerApprovalViewModel
        {
            Id = customer.Id,
            LegalName = customer.LegalName,
            TradeName = customer.TradeName,
            Cnpj = customer.Cnpj,
            Status = customer.Status,
            MinimumOrder = customer.MinimumOrder,
            CreditLimit = customer.CreditLimit,
            WmcCode = customer.WmcCode,
            PriceTableId = customer.PriceTableId,
            SalesRepresentativeId = customer.SalesRepresentativeId,
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
            PaymentTerms = await db.PaymentTerms
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToListAsync()
        };
    }
}
