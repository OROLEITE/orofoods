using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Catalog;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class CategoriesController(ApplicationDbContext db, AdminCatalogService catalogService) : Controller
{
    public async Task<IActionResult> Index() => View(await db.ProductCategories.AsNoTracking().Include(x => x.Products).OrderBy(x => x.SortOrder).ToListAsync());

    public async Task<IActionResult> Edit(int? id)
    {
        var category = id is null ? new ProductCategory() : await db.ProductCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductCategory input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Slug)) ModelState.AddModelError(string.Empty, "Nome e identificador sao obrigatorios.");
        if (!ModelState.IsValid) return View(input);
        try { await catalogService.SaveCategoryAsync(input); }
        catch (InvalidOperationException error) { ModelState.AddModelError(string.Empty, error.Message); return View(input); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await catalogService.DeactivateCategoryAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
