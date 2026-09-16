using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class PaymentTermsController(ApplicationDbContext db, AdminCommercialService service) : Controller
{
    public async Task<IActionResult> Index() => View(await db.PaymentTerms.AsNoTracking().Include(x => x.CustomerPaymentTerms).OrderBy(x => x.SortOrder).ToListAsync());
    public async Task<IActionResult> Edit(int? id) { var entity = id is null ? new PaymentTerm() : await db.PaymentTerms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); return entity is null ? NotFound() : View(entity); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentTerm input) { if (string.IsNullOrWhiteSpace(input.Name)) { ModelState.AddModelError(string.Empty, "Nome obrigatorio."); return View(input); } await service.SavePaymentTermAsync(input); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id) { await service.DeactivatePaymentTermAsync(id); return RedirectToAction(nameof(Index)); }
}
