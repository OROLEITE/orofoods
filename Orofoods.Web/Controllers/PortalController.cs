using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Controllers;

public class PortalController(ApplicationDbContext db) : Controller
{
    private const int DemoCustomerId = 1;

    public async Task<IActionResult> Dashboard()
    {
        var customer = await db.Customers.Include(x => x.Addresses).SingleAsync(x => x.Id == DemoCustomerId);
        var last = await db.Orders.Include(x => x.Items).ThenInclude(x => x.Product).OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.CustomerId == DemoCustomerId);
        return View(new DashboardVm { Customer = customer, LastOrder = last, OpenOrders = await db.Orders.CountAsync(x => x.CustomerId == DemoCustomerId && x.Status != OrderStatus.Delivered && x.Status != OrderStatus.Cancelled), MonthTotal = await db.Orders.Where(x => x.CustomerId == DemoCustomerId && x.CreatedAt.Month == DateTime.Now.Month).SumAsync(x => (decimal?)x.Total) ?? 0 });
    }

    public async Task<IActionResult> Catalog(string? q, string? category)
    {
        var query = db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q) || x.Sku.Contains(q));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        ViewBag.Prices = await db.CustomerPrices.Where(x => x.CustomerId == DemoCustomerId).ToDictionaryAsync(x => x.ProductId, x => x.Price);
        return View(await query.OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync());
    }

    public async Task<IActionResult> Repeat(int id)
    {
        var customer = await db.Customers.Include(x => x.Addresses).SingleAsync(x => x.Id == DemoCustomerId);
        var order = await db.Orders.Include(x => x.Items).ThenInclude(x => x.Product).SingleAsync(x => x.Id == id && x.CustomerId == DemoCustomerId);
        var prices = await db.CustomerPrices.Where(x => x.CustomerId == DemoCustomerId).ToDictionaryAsync(x => x.ProductId, x => x.Price);
        var productIds = order.Items.Select(x => x.ProductId).ToList();
        var products = await db.Products.Include(x => x.SubstituteProduct).Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var items = order.Items.Select(x => { var p = products[x.ProductId]; return new RepeatItemVm(p.Id, p.Sku, p.Name, p.UnitDescription, p.MinimumCases, p.IsAvailable ? x.Quantity : 0, prices.GetValueOrDefault(p.Id, p.BasePrice), p.IsAvailable, p.SubstituteProduct?.Name); }).ToList();
        return View(new RepeatOrderVm { Customer = customer, SourceOrder = order, Items = items, Addresses = customer.Addresses });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ConfirmOrderVm input)
    {
        var customer = await db.Customers.SingleAsync(x => x.Id == DemoCustomerId);
        var products = await db.Products.Where(x => input.ProductIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var prices = await db.CustomerPrices.Where(x => x.CustomerId == DemoCustomerId).ToDictionaryAsync(x => x.ProductId, x => x.Price);
        var order = new Order { CustomerId = DemoCustomerId, DeliveryAddressId = input.AddressId, RequestedDeliveryDate = input.RequestedDate, Status = OrderStatus.Received, PaymentMethod = input.PaymentMethod, Notes = input.Notes, CreatedAt = DateTime.Now };
        for (var i = 0; i < Math.Min(input.ProductIds.Length, input.Quantities.Length); i++)
        {
            if (!products.TryGetValue(input.ProductIds[i], out var p) || !p.IsAvailable || input.Quantities[i] <= 0) continue;
            var qty = Math.Max(input.Quantities[i], p.MinimumCases);
            order.Items.Add(new OrderItem { ProductId = p.Id, Quantity = qty, UnitPrice = prices.GetValueOrDefault(p.Id, p.BasePrice) });
        }
        order.Total = order.Items.Sum(x => x.Quantity * x.UnitPrice);
        var error = order.Total < customer.MinimumOrder ? $"O pedido mínimo é {customer.MinimumOrder:C}." : order.Total > customer.CreditLimit - customer.CreditUsed ? "O total ultrapassa o crédito disponível." : !order.Items.Any() ? "Inclua ao menos um item disponível." : null;
        if (error is not null) return RedirectToAction(nameof(Repeat), new { id = input.SourceOrderId, error });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        order.Number = $"ORO-{DateTime.Now:yyyy}-{order.Id:000000}";
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Success), new { id = order.Id });
    }

    public async Task<IActionResult> Success(int id) => View(await db.Orders.Include(x => x.Items).ThenInclude(x => x.Product).Include(x => x.DeliveryAddress).SingleAsync(x => x.Id == id && x.CustomerId == DemoCustomerId));
}
