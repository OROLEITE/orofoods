using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Services.Catalog;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminProductsControllerTests
{
    [Fact]
    public async Task Edit_returns_validation_error_when_wmc_code_is_already_assigned()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.ProductCategories.Add(category);
        db.Products.Add(new Product
        {
            Sku = "WMC-243680",
            WmcCode = " 243680 ",
            Name = "Produto já mapeado",
            ProductCategory = category,
            Brand = "Teste",
            Unit = "UN"
        });
        await db.SaveChangesAsync();

        var controller = new ProductsController(db, new AdminCatalogService(db), Mock.Of<IProductImageStorage>());
        var result = await controller.Edit(new Product
        {
            Sku = "STG-TEST-0001",
            WmcCode = "243680",
            Name = "Produto de homologação",
            ProductCategoryId = category.Id,
            Brand = "Teste",
            Unit = "UN"
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState.Values.SelectMany(state => state.Errors),
            error => error.ErrorMessage.Contains("codigo WMC", StringComparison.OrdinalIgnoreCase));
        Assert.Single(await db.Products.ToListAsync());
    }
}
