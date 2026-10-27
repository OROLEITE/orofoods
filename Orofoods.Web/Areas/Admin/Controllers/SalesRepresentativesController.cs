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
    public async Task<IActionResult> Edit(SalesRepresentative input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            ModelState.AddModelError(nameof(input.Name), "Nome obrigatório.");
        }

        var normalizedWmcCode = string.IsNullOrWhiteSpace(input.WmcCode)
            ? null
            : input.WmcCode.Trim().ToUpperInvariant();
        if (normalizedWmcCode is not null)
        {
            var duplicate = await db.SalesRepresentatives
                .AsNoTracking()
                .AnyAsync(item => item.Id != input.Id
                    && item.WmcCode != null
                    && item.WmcCode.Trim().ToUpper() == normalizedWmcCode, cancellationToken);
            if (duplicate)
            {
                ModelState.AddModelError(string.Empty, "Código WMC já está vinculado a outro vendedor.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        try
        {
            await service.SaveSalesRepresentativeAsync(input, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("WmcCode", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível salvar o Código WMC. Verifique se a migration do campo foi aplicada no banco.");
            return View(input);
        }

        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id) { await service.DeactivateSalesRepresentativeAsync(id); return RedirectToAction(nameof(Index)); }
}
