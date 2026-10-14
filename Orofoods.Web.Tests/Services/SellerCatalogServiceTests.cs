using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Sellers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class SellerCatalogServiceTests
{
    [Fact]
    public async Task Catalog_uses_customer_price_and_only_commercially_available_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var table = new PriceTable { Name = "Tabela A", IsActive = true };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative, PriceTable = table };
        var available = Product("AVAILABLE", "Pao disponivel", true);
        var unavailable = Product("EMPTY", "Pao sem estoque", true);
        var inactive = Product("INACTIVE", "Pao inativo", false);
        db.AddRange(representative, seller, table, customer, available, unavailable, inactive, new PriceTableItem { PriceTable = table, Product = available, Price = 42m });
        await db.SaveChangesAsync();
        db.ProductInventories.AddRange(
            new ProductInventory { ProductId = available.Id, QuantityOnHand = 10 },
            new ProductInventory { ProductId = unavailable.Id, QuantityOnHand = 0 },
            new ProductInventory { ProductId = inactive.Id, QuantityOnHand = 10 });
        await db.SaveChangesAsync();

        var result = await new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db))
            .GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, null);

        var product = Assert.Single(Assert.IsType<SellerCatalogViewModel>(result).Products);
        Assert.Equal(available.Id, product.Id);
        Assert.Equal(42m, product.UnitPrice);
    }

    [Fact]
    public async Task Catalog_rejects_a_customer_outside_the_sellers_portfolio()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representativeA = new SalesRepresentative { Name = "Vendedor A", IsActive = true };
        var representativeB = new SalesRepresentative { Name = "Vendedor B", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representativeA };
        var customer = new Customer { LegalName = "Cliente B", TradeName = "Cliente B", Cnpj = "22.222.222/0001-22", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representativeB };
        db.AddRange(representativeA, representativeB, seller, customer);
        await db.SaveChangesAsync();

        var result = await new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db))
            .GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task Catalog_searches_by_ean_and_limits_results_to_requested_page()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "33.333.333/0001-33", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, seller, customer);
        var products = Enumerable.Range(1, 25).Select(index =>
        {
            var product = Product($"SKU-{index:00}", $"Produto {index:00}", true);
            product.Ean = $"789000000{index.ToString("D5")}";
            return product;
        }).ToArray();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        db.ProductInventories.AddRange(products.Select(product => new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 }));
        await db.SaveChangesAsync();
        Assert.Equal("78900000000001", products[0].Ean);

        var service = new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db));
        var secondPage = await service.GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, "789000000", page: 2, pageSize: 20);
        var eanMatch = await service.GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, "78900000000001", page: 1, pageSize: 20);

        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Equal(5, secondPage.Products.Count);
        Assert.Single(Assert.IsType<SellerCatalogViewModel>(eanMatch).Products);
    }

    [Fact]
    public async Task Catalog_clamps_page_size_to_prevent_unbounded_product_results()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "44.444.444/0001-44", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, seller, customer);
        var products = Enumerable.Range(1, 30).Select(index => Product($"SKU-{index:00}", $"Produto {index:00}", true)).ToArray();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        db.ProductInventories.AddRange(products.Select(product => new ProductInventory { ProductId = product.Id, QuantityOnHand = 10 }));
        await db.SaveChangesAsync();

        var service = new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db));
        var legacyCatalog = await service.GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, null);
        var result = await service.GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, null, page: 1, pageSize: 1000);

        Assert.NotNull(result);
        Assert.Equal(20, result.Products.Count);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(30, Assert.IsType<SellerCatalogViewModel>(legacyCatalog).Products.Count);
    }

    private static Product Product(string sku, string name, bool active) => new()
    {
        Sku = sku,
        Name = name,
        Brand = "Orofoods",
        BasePrice = 100m,
        MinimumCases = 1,
        IsActive = active,
        IsAvailable = true,
        ProductCategory = new ProductCategory { Name = "Pães", Slug = sku.ToLowerInvariant() }
    };

    private static ClaimsPrincipal SellerPrincipal(string userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Vendedor")],
        "test"));
}
