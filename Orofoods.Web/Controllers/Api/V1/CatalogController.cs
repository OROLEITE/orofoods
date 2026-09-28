using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Api;
using Orofoods.Web.Data;

namespace Orofoods.Web.Controllers.Api.V1;

[ApiController]
[Route("api/v1/catalog")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("api")]
public class CatalogController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<ProductResponse>>> Products(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.Products.AsNoTracking().Where(product => product.IsActive);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.Include(product => product.ProductCategory).Include(product => product.Images)
            .OrderBy(product => product.Name).Skip(request.Skip).Take(request.PageSize)
            .Select(product => new ProductProjection(product.Id, product.Sku, product.Name, product.Brand, product.Description,
                product.ProductCategory!.Name, product.UnitsPerCase,
                product.IsAvailable && db.ProductInventories.Any(inventory => inventory.ProductId == product.Id && inventory.QuantityOnHand > inventory.QuantityReserved),
                product.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder).Select(image => (int?)image.Id).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var responses = items.Select(product => new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Brand,
            product.Description,
            product.Category,
            product.UnitsPerCase,
            product.IsAvailable,
            product.ImageId is int imageId ? Url.RouteUrl("ProductMedia", new { imageId }) : null)).ToList();
        return Ok(new PagedResult<ProductResponse>(responses, request.Page, request.PageSize, totalItems));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<PagedResult<CategoryResponse>>> Categories(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.ProductCategories.AsNoTracking().Where(category => category.IsActive);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(category => category.SortOrder).Skip(request.Skip).Take(request.PageSize)
            .Select(category => new CategoryResponse(category.Id, category.Name, category.Slug)).ToListAsync(cancellationToken);
        return Ok(new PagedResult<CategoryResponse>(items, request.Page, request.PageSize, totalItems));
    }
}

public sealed record ProductResponse(int Id, string Sku, string Name, string Brand, string Description, string Category, int UnitsPerCase, bool IsAvailable, string? ImageUrl);
internal sealed record ProductProjection(int Id, string Sku, string Name, string Brand, string Description, string Category, int UnitsPerCase, bool IsAvailable, int? ImageId);
public sealed record CategoryResponse(int Id, string Name, string Slug);
