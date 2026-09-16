using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class PriceTablesController(ApplicationDbContext db, AdminCommercialService service) : Controller
{
    public async Task<IActionResult> Index() => View(await db.PriceTables.AsNoTracking().Include(x => x.Customers).Include(x => x.Items).OrderBy(x => x.Name).ToListAsync());
    public async Task<IActionResult> Edit(int? id) { var entity = id is null ? new PriceTable() : await db.PriceTables.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); return entity is null ? NotFound() : View(entity); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PriceTable input) { if (string.IsNullOrWhiteSpace(input.Name)) { ModelState.AddModelError(string.Empty, "Nome obrigatorio."); return View(input); } await service.SavePriceTableAsync(input); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> Items(int id)
    {
        var table = await db.PriceTables.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (table is null) return NotFound();
        return View(new AdminPriceTableItemsViewModel { PriceTable = table, Products = await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(), Prices = await db.PriceTableItems.AsNoTracking().Where(x => x.PriceTableId == id).ToDictionaryAsync(x => x.ProductId) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePrice(int priceTableId, int productId, decimal price, decimal? promotionalPrice) { await service.SavePriceAsync(priceTableId, productId, price, promotionalPrice); return RedirectToAction(nameof(Items), new { id = priceTableId }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id) { await service.DeactivatePriceTableAsync(id); return RedirectToAction(nameof(Index)); }
}
