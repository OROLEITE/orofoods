using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class SalesRepresentativesController(ApplicationDbContext db, AdminCommercialService service) : Controller
{
    public async Task<IActionResult> Index() => View(await db.SalesRepresentatives.AsNoTracking().Include(x => x.Customers).OrderBy(x => x.Name).ToListAsync());
    public async Task<IActionResult> Edit(int? id) { var entity = id is null ? new SalesRepresentative() : await db.SalesRepresentatives.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); return entity is null ? NotFound() : View(entity); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SalesRepresentative input) { if (string.IsNullOrWhiteSpace(input.Name)) { ModelState.AddModelError(string.Empty, "Nome obrigatorio."); return View(input); } await service.SaveSalesRepresentativeAsync(input); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id) { await service.DeactivateSalesRepresentativeAsync(id); return RedirectToAction(nameof(Index)); }
}
