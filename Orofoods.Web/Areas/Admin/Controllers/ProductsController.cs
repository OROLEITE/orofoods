using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class ProductsController(ApplicationDbContext db, AdminCatalogService catalogService, IProductImageStorage productImageStorage) : Controller
{
    public async Task<IActionResult> Index(string? q, bool includeInactive = false, int? page = null)
    {
        var query = db.Products.AsNoTracking().Include(x => x.ProductCategory).Include(x => x.Images).AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q) || x.Sku.Contains(q) || x.Brand.Contains(q));
        var products = await query.OrderBy(x => x.Name).ToListAsync();
        var productIds = products.Select(product => product.Id).ToArray();
        ViewBag.StockByProductId = await db.ProductInventories
            .AsNoTracking()
            .Where(inventory => productIds.Contains(inventory.ProductId))
            .ToDictionaryAsync(inventory => inventory.ProductId, inventory => inventory.QuantityOnHand);
        SetListContext(q, includeInactive, page);
        return View(products);
    }

    public async Task<IActionResult> Edit(int? id, string? q = null, bool includeInactive = false, int? page = null)
    {
        var product = id is null ? new Product() : await db.Products.AsNoTracking().Include(x => x.Images).SingleOrDefaultAsync(x => x.Id == id);
        if (product is null) return NotFound();
        SetListContext(q, includeInactive, page);
        await PopulateSelectionsAsync(product);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Product input, string? q = null, bool includeInactive = false, int? page = null)
    {
        SetListContext(q, includeInactive, page);
        if (string.IsNullOrWhiteSpace(input.Sku) || string.IsNullOrWhiteSpace(input.Name) || input.ProductCategoryId == 0)
            ModelState.AddModelError(string.Empty, "SKU, nome e categoria são obrigatórios.");
        if (!ModelState.IsValid) { await PopulateSelectionsAsync(input); return View(input); }

        var wasActive = input.Id == 0
            ? null
            : await db.Products.AsNoTracking()
                .Where(product => product.Id == input.Id)
                .Select(product => (bool?)product.IsActive)
                .SingleOrDefaultAsync();
        try { await catalogService.SaveProductAsync(input); }
        catch (InvalidOperationException error) { ModelState.AddModelError(string.Empty, error.Message); await PopulateSelectionsAsync(input); return View(input); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível salvar as alterações do produto. Tente novamente.");
            await PopulateSelectionsAsync(input);
            return View(input);
        }

        TempData["ProductSuccess"] = wasActive.HasValue && wasActive.Value != input.IsActive
            ? $"Produto {(input.IsActive ? "ativado" : "desativado")} com sucesso."
            : input.Id == 0 ? "Produto cadastrado com sucesso." : "Produto salvo com sucesso.";
        return RedirectToAction(nameof(Index), new { q, includeInactive, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, string? q = null, bool includeInactive = false, int? page = null)
    {
        if (!await db.Products.AnyAsync(product => product.Id == id))
        {
            TempData["ProductError"] = "Produto não encontrado; o status não foi alterado.";
            return RedirectToAction(nameof(Index), new { q, includeInactive, page });
        }

        try
        {
            await catalogService.DeactivateProductAsync(id);
            TempData["ProductSuccess"] = "Produto desativado com sucesso.";
        }
        catch (InvalidOperationException)
        {
            TempData["ProductError"] = "Não foi possível desativar o produto. O status não foi alterado.";
        }
        catch (DbUpdateException)
        {
            TempData["ProductError"] = "Não foi possível salvar a desativação do produto. Tente novamente.";
        }

        return RedirectToAction(nameof(Index), new { q, includeInactive, page });
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

    private void SetListContext(string? q, bool includeInactive, int? page)
    {
        ViewBag.Query = q;
        ViewBag.IncludeInactive = includeInactive;
        ViewBag.Page = page;
    }
}
