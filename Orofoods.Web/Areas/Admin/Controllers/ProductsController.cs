using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class ProductsController(ApplicationDbContext db, AdminCatalogService catalogService, IProductImageStorage productImageStorage) : Controller
{
    private const int ProductsPageSize = 25;
    private static readonly MethodInfo StringReplaceMethod = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo StringContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StringToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly (string Characters, string Replacement)[] SearchCharacterReplacements =
    [
        ("àáâãäåÀÁÂÃÄÅ", "a"),
        ("èéêëÈÉÊË", "e"),
        ("ìíîïÌÍÎÏ", "i"),
        ("òóôõöøÒÓÔÕÖØ", "o"),
        ("ùúûüÙÚÛÜ", "u"),
        ("çÇ", "c"),
        ("ñÑ", "n"),
        ("ýÿÝŸ", "y"),
        ("æÆ", "ae"),
        ("œŒ", "oe"),
        ("ßẞ", "ss")
    ];

    public async Task<IActionResult> Index(string? q, bool includeInactive = false, int? page = null)
    {
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var normalizedSearch = NormalizeSearchTerm(q);
        if (!string.IsNullOrWhiteSpace(q))
        {
            if (normalizedSearch.Length == 0)
                query = query.Where(_ => false);
            else
                query = query.Where(CreateProductSearchPredicate(normalizedSearch));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)ProductsPageSize);
        var currentPage = totalPages == 0 ? 1 : Math.Clamp(page.GetValueOrDefault(1), 1, totalPages);
        var products = await query
            .Include(x => x.ProductCategory)
            .Include(x => x.Images)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((currentPage - 1) * ProductsPageSize)
            .Take(ProductsPageSize)
            .ToListAsync();
        var productIds = products.Select(product => product.Id).ToArray();
        ViewBag.StockByProductId = await db.ProductInventories
            .AsNoTracking()
            .Where(inventory => productIds.Contains(inventory.ProductId))
            .ToDictionaryAsync(inventory => inventory.ProductId, inventory => inventory.QuantityOnHand);
        ViewBag.TotalItems = totalItems;
        ViewBag.TotalPages = totalPages;
        SetListContext(q, includeInactive, currentPage);
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
        Product? existingProduct = null;
        if (input.Id != 0)
        {
            existingProduct = await db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == input.Id);
            if (existingProduct is null) return NotFound();
            RestoreNonEditableProductFields(input, existingProduct);
        }

        foreach (var field in NonEditableProductFieldNames)
            ModelState.Remove(field);

        if (string.IsNullOrWhiteSpace(input.Sku))
            ModelState.AddModelError(nameof(input.Sku), "Informe o SKU do produto.");
        if (string.IsNullOrWhiteSpace(input.Name))
            ModelState.AddModelError(nameof(input.Name), "Informe o nome do produto.");
        if (input.ProductCategoryId == 0)
            ModelState.AddModelError(nameof(input.ProductCategoryId), "Selecione uma categoria para o produto.");
        if (!ModelState.IsValid) { await PopulateSelectionsAsync(input); return View(input); }

        var wasActive = existingProduct?.IsActive;
        Product savedProduct;
        try { savedProduct = await catalogService.SaveProductAsync(input); }
        catch (InvalidOperationException error) { ModelState.AddModelError(string.Empty, error.Message); await PopulateSelectionsAsync(input); return View(input); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível salvar as alterações do produto. Tente novamente.");
            await PopulateSelectionsAsync(input);
            return View(input);
        }

        TempData["ProductSuccess"] = wasActive.HasValue && wasActive.Value != savedProduct.IsActive
            ? $"Produto {(savedProduct.IsActive ? "ativado" : "desativado")} com sucesso."
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

    private static readonly string[] NonEditableProductFieldNames =
    [
        nameof(Product.Description),
        nameof(Product.Weight),
        nameof(Product.Unit),
        nameof(Product.UnitsPerPackage),
        nameof(Product.PromotionalPrice),
        nameof(Product.IsFeatured),
        nameof(Product.IsPromotional),
        nameof(Product.Accent)
    ];

    private static void RestoreNonEditableProductFields(Product input, Product existingProduct)
    {
        input.Description = existingProduct.Description;
        input.Weight = existingProduct.Weight;
        input.Unit = existingProduct.Unit;
        input.UnitsPerPackage = existingProduct.UnitsPerPackage;
        input.PromotionalPrice = existingProduct.PromotionalPrice;
        input.IsFeatured = existingProduct.IsFeatured;
        input.IsPromotional = existingProduct.IsPromotional;
        input.Accent = existingProduct.Accent;
    }

    private static Expression<Func<Product, bool>> CreateProductSearchPredicate(string searchTerm)
    {
        var product = Expression.Parameter(typeof(Product), "product");
        Expression? predicate = null;
        foreach (var propertyName in new[] { nameof(Product.Name), nameof(Product.Sku), nameof(Product.WmcCode), nameof(Product.Brand) })
        {
            var field = Expression.Property(product, propertyName);
            var fieldIsNotNull = Expression.NotEqual(field, Expression.Constant(null, typeof(string)));
            var normalizedField = NormalizeDatabaseSearchText(field);
            var containsSearchTerm = Expression.Call(normalizedField, StringContainsMethod, Expression.Constant(searchTerm));
            var fieldMatches = Expression.AndAlso(fieldIsNotNull, containsSearchTerm);
            predicate = predicate is null ? fieldMatches : Expression.OrElse(predicate, fieldMatches);
        }

        return Expression.Lambda<Func<Product, bool>>(predicate!, product);
    }

    private static Expression NormalizeDatabaseSearchText(Expression field)
    {
        Expression normalized = field;
        foreach (var (characters, replacement) in SearchCharacterReplacements)
        {
            foreach (var character in characters)
            {
                normalized = Expression.Call(
                    normalized,
                    StringReplaceMethod,
                    Expression.Constant(character.ToString()),
                    Expression.Constant(replacement));
            }
        }

        return Expression.Call(normalized, StringToLowerMethod);
    }

    private static string NormalizeSearchTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return withoutDiacritics.Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Replace("œ", "oe", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
    }
}
