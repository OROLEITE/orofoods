using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Data;

public class SeedDataTests
{
    [Fact]
    public void Bimbo_seed_uses_correct_portuguese_accentuation()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var seed = File.ReadAllText(Path.Combine(projectPath, "Data", "SeedData.cs"));

        Assert.Contains("Mini Pão de Hambúrguer Congelado", seed);
        Assert.Contains("porções especiais", seed);
        Assert.Contains("existingProduct.Name = product.Name", seed);
    }

    [Fact]
    public void Bimbo_seed_includes_extended_frozen_bread_catalog_with_loose_bread_images()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var seed = File.ReadAllText(Path.Combine(projectPath, "Data", "SeedData.cs"));

        Assert.Contains("Sku = \"BIM-004\"", seed);
        Assert.Contains("Sku = \"BIM-005\"", seed);
        Assert.Contains("Sku = \"BIM-006\"", seed);
        Assert.Contains("Sku = \"BIM-007\"", seed);
        Assert.Contains("Sku = \"BIM-008\"", seed);
        Assert.Contains("bimbo-brioche-loose.png", seed);
        Assert.Contains("bimbo-gergelim-loose.png", seed);
        Assert.Contains("bimbo-australiano-loose.png", seed);
        Assert.Contains("existingImage.Url = item.Value", seed);
    }

    [Fact]
    public void Seed_deactivates_legacy_example_products_without_images()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var seed = File.ReadAllText(Path.Combine(projectPath, "Data", "SeedData.cs"));

        Assert.Contains("legacyExampleSkus", seed);
        Assert.Contains("existingExample.IsActive = false", seed);
        Assert.Contains("\"PAO-001\"", seed);
        Assert.Contains("\"MOL-018\"", seed);
    }

    [Fact]
    public void Product_detail_views_render_the_primary_image_and_description()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var publicDetail = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "Product.cshtml"));
        var portalDetail = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Product.cshtml"));

        Assert.Contains("var primaryImage", publicDetail);
        Assert.Contains("Url.RouteUrl(\"ProductMedia\", new { imageId = primaryImage.Id })", publicDetail);
        Assert.Contains("Descri&#231;&#227;o do produto", publicDetail);
        Assert.Contains("var primaryImage", portalDetail);
        Assert.Contains("Url.RouteUrl(\"ProductMedia\", new { imageId = primaryImage.Id })", portalDetail);
        Assert.Contains("Descrição do produto", portalDetail);
    }

    [Fact]
    public void Carousel_highlights_use_distinct_bimbo_product_images()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var seed = File.ReadAllText(Path.Combine(projectPath, "Data", "SeedData.cs"));

        Assert.Contains("bimbo-smart-loose.png", seed);
        Assert.Contains("[\"BIM-004\"] = \"/images/products/bimbo-smart-loose.png\"", seed);
        Assert.Contains("Sku = \"BIM-008\"", seed);
        Assert.Contains("IsFeatured = true, Accent = \"#47382b\"", seed);
    }

    [Fact]
    public void Seeded_customers_use_a_one_hundred_real_minimum_order()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var seed = File.ReadAllText(Path.Combine(projectPath, "Data", "SeedData.cs"));

        Assert.DoesNotContain("MinimumOrder = 650m", seed);
        Assert.DoesNotContain("MinimumOrder = 300m", seed);
        Assert.Contains("MinimumOrder = 100m", seed);
    }

    [Fact]
    public void How_it_works_uses_a_connected_modern_process_layout()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Views", "Home", "HowItWorks.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("process-flow", view);
        Assert.Contains("process-step", view);
        Assert.Contains("An&aacute;lise comercial", view);
        Assert.Contains("process-flow", styles);
        Assert.Contains("process-step", styles);
    }

    [Fact]
    public async Task Ensure_product_categories_adds_required_slugs_when_legacy_category_exists()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.ProductCategories.Add(new ProductCategory { Name = "Migrado", Slug = "migrado", IsActive = true });
        await db.SaveChangesAsync();

        await SeedData.EnsureProductCategoriesAsync(db);

        Assert.Contains(await db.ProductCategories.ToListAsync(), x => x.Slug == "paes");
        Assert.Contains(await db.ProductCategories.ToListAsync(), x => x.Slug == "molhos");
    }
}
