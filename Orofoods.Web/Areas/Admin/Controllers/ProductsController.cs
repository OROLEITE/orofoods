using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class ProductsController(ApplicationDbContext db, AdminCatalogService catalogService, IProductImageStorage productImageStorage) : Controller
{
    public async Task<IActionResult> Index(string? q, bool includeInactive = false)
    {
        var query = db.Products.AsNoTracking().Include(x => x.ProductCategory).Include(x => x.Images).AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q) || x.Sku.Contains(q) || x.Brand.Contains(q));
        ViewBag.Query = q;
        ViewBag.IncludeInactive = includeInactive;
        return View(await query.OrderBy(x => x.Name).ToListAsync());
    }

    public async Task<IActionResult> Edit(int? id)
    {
        var product = id is null ? new Product() : await db.Products.AsNoTracking().Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == id);
        if (product is null) return NotFound();
        await PopulateSelectionsAsync(product);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Product input)
    {
        if (string.IsNullOrWhiteSpace(input.Sku) || string.IsNullOrWhiteSpace(input.Name) || input.ProductCategoryId == 0)
            ModelState.AddModelError(string.Empty, "SKU, nome e categoria sao obrigatorios.");
        if (!ModelState.IsValid) { await PopulateSelectionsAsync(input); return View(input); }
        try { await catalogService.SaveProductAsync(input); }
        catch (InvalidOperationException error) { ModelState.AddModelError(string.Empty, error.Message); await PopulateSelectionsAsync(input); return View(input); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        await catalogService.DeactivateProductAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(int productId, IFormFile image, string? altText)
    {
        if (image is null || image.Length == 0 || image.Length > 5 * 1024 * 1024 || !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ImageError"] = "Envie uma imagem de ate 5 MB.";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
        {
            TempData["ImageError"] = "Use arquivos JPG, PNG ou WEBP.";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        await using var stream = image.OpenReadStream();
        var storedUrl = await productImageStorage.SaveAsync(stream, image.FileName, image.ContentType);

        try
        {
            var product = await db.Products.AsNoTracking().SingleAsync(item => item.Id == productId);
            await catalogService.AddProductImageAsync(productId, storedUrl, string.IsNullOrWhiteSpace(altText) ? product.Name : altText.Trim());
        }
        catch
        {
            await productImageStorage.DeleteAsync(storedUrl);
            throw;
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int productId, int imageId)
    {
        await catalogService.SetPrimaryImageAsync(productId, imageId);
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveImage(int productId, int imageId)
    {
        var image = await db.ProductImages.SingleOrDefaultAsync(item => item.ProductId == productId && item.Id == imageId);
        if (image is not null)
        {
            await catalogService.RemoveProductImageAsync(productId, imageId);
            await productImageStorage.DeleteAsync(image.Url);
        }

        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    private async Task PopulateSelectionsAsync(Product product)
    {
        ViewBag.Categories = new SelectList(await db.ProductCategories.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(), "Id", "Name", product.ProductCategoryId);
        ViewBag.Substitutes = new SelectList(await db.Products.Where(x => x.IsActive && x.Id != product.Id).OrderBy(x => x.Name).ToListAsync(), "Id", "Name", product.SubstituteProductId);
    }
}
