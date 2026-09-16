using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Catalog;

public class AdminCatalogService(ApplicationDbContext db)
{
    public async Task<Product> SaveProductAsync(Product input, CancellationToken cancellationToken = default)
    {
        var sku = input.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(x => x.Id != input.Id && x.Sku.ToUpper() == sku, cancellationToken))
        {
            throw new InvalidOperationException("Ja existe um produto com esse SKU.");
        }

        var product = input.Id == 0
            ? new Product()
            : await db.Products.SingleAsync(x => x.Id == input.Id, cancellationToken);
        product.Sku = sku;
        product.WmcCode = string.IsNullOrWhiteSpace(input.WmcCode) ? null : input.WmcCode.Trim();
        product.Name = input.Name.Trim();
        product.ProductCategoryId = input.ProductCategoryId;
        product.Brand = input.Brand.Trim();
        product.Description = input.Description.Trim();
        product.Weight = input.Weight;
        product.Unit = input.Unit.Trim();
        product.UnitsPerPackage = input.UnitsPerPackage;
        product.UnitsPerCase = input.UnitsPerCase;
        product.MinimumCases = Math.Max(1, input.MinimumCases);
        product.BasePrice = input.BasePrice;
        product.PromotionalPrice = input.PromotionalPrice;
        product.IsAvailable = input.IsAvailable;
        product.IsFeatured = input.IsFeatured;
        product.IsPromotional = input.IsPromotional;
        product.SubstituteProductId = input.SubstituteProductId;
        product.IsActive = input.IsActive;
        product.Accent = input.Accent;
        if (input.Id == 0) db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task DeactivateProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleAsync(x => x.Id == id, cancellationToken);
        product.IsActive = false;
        product.IsAvailable = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductImage> AddProductImageAsync(int productId, string url, string altText, CancellationToken cancellationToken = default)
    {
        var images = await db.ProductImages.Where(image => image.ProductId == productId).ToListAsync(cancellationToken);
        var image = new ProductImage
        {
            ProductId = productId,
            Url = url,
            AltText = altText,
            SortOrder = images.Count == 0 ? 0 : images.Max(item => item.SortOrder) + 1,
            IsPrimary = images.Count == 0
        };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);
        return image;
    }

    public async Task SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default)
    {
        var images = await db.ProductImages.Where(image => image.ProductId == productId).ToListAsync(cancellationToken);
        foreach (var image in images) image.IsPrimary = image.Id == imageId;
        if (!images.Any(image => image.IsPrimary)) throw new InvalidOperationException("Imagem do produto nao encontrada.");
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveProductImageAsync(int productId, int imageId, CancellationToken cancellationToken = default)
    {
        var image = await db.ProductImages.SingleOrDefaultAsync(item => item.ProductId == productId && item.Id == imageId, cancellationToken)
            ?? throw new InvalidOperationException("Imagem do produto nao encontrada.");
        var wasPrimary = image.IsPrimary;
        db.ProductImages.Remove(image);
        await db.SaveChangesAsync(cancellationToken);
        if (wasPrimary)
        {
            var nextImage = await db.ProductImages.Where(item => item.ProductId == productId).OrderBy(item => item.SortOrder).FirstOrDefaultAsync(cancellationToken);
            if (nextImage is not null)
            {
                nextImage.IsPrimary = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task<ProductCategory> SaveCategoryAsync(ProductCategory input, CancellationToken cancellationToken = default)
    {
        var slug = input.Slug.Trim().ToLowerInvariant();
        if (await db.ProductCategories.AnyAsync(x => x.Id != input.Id && x.Slug == slug, cancellationToken))
        {
            throw new InvalidOperationException("Ja existe uma categoria com esse identificador.");
        }

        var category = input.Id == 0 ? new ProductCategory() : await db.ProductCategories.SingleAsync(x => x.Id == input.Id, cancellationToken);
        category.Name = input.Name.Trim();
        category.Slug = slug;
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;
        if (input.Id == 0) db.ProductCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task DeactivateCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await db.ProductCategories.SingleAsync(x => x.Id == id, cancellationToken);
        category.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }
}
