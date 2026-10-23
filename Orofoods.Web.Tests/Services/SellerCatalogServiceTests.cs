using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task Catalog_uses_customer_price_and_lists_active_products_regardless_of_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var table = new PriceTable { Name = "Tabela A", IsActive = true };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative, PriceTable = table };
        var available = Product("AVAILABLE", "Pao disponivel", true);
        available.BasePrice = 50m;
        available.PromotionalPrice = 35m;
        available.Brand = "BIMBO";
        available.WmcCode = "503813";
        available.CommercialUnitCode = "PCT";
        available.LogisticsUnitCode = "CX";
        available.PackagesPerCase = 8;
        var unavailable = Product("EMPTY", "Pao sem estoque", true);
        var inactive = Product("INACTIVE", "Pao inativo", false);
        db.AddRange(representative, seller, table, customer, available, unavailable, inactive, new PriceTableItem { PriceTable = table, Product = available, Price = 42m });
        await db.SaveChangesAsync();
        db.ProductInventories.AddRange(
            new ProductInventory { ProductId = available.Id, QuantityOnHand = 10, QuantityReserved = 3 },
            new ProductInventory { ProductId = unavailable.Id, QuantityOnHand = 0 },
            new ProductInventory { ProductId = inactive.Id, QuantityOnHand = 10 });
        await db.SaveChangesAsync();

        var result = await new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db))
            .GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, null);

        var products = Assert.IsType<SellerCatalogViewModel>(result).Products;
        Assert.Equal(2, products.Count);
        Assert.DoesNotContain(products, x => x.Id == inactive.Id);
        var availableRow = products.Single(x => x.Id == available.Id);
        Assert.Equal(50m, availableRow.UnitPrice);
        Assert.Equal("BIMBO", availableRow.Brand);
        Assert.Equal("503813", availableRow.WmcCode);
        Assert.Equal("PCT", availableRow.CommercialUnitCode);
        Assert.Equal("PCT", availableRow.PriceUnitLabel);
        Assert.Equal(7, availableRow.AvailableQuantity);
        Assert.Contains(products, x => x.Id == unavailable.Id);
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
    public async Task Catalog_search_is_case_and_accent_insensitive_across_name_sku_wmc_code_and_ean()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "55.555.555/0001-55", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
        var baudUcco = Product("SKU-BAUDUCCO-390", "BAUDUCCO PÃO GRÃOS CLÁSSICOS 390G", true);
        baudUcco.WmcCode = "WMC-BAU-390";
        baudUcco.Brand = "BIMBO";
        baudUcco.Ean = "7891234567890";
        baudUcco.Ean13 = "0123456789012";
        baudUcco.Dun14 = "00012345678905";
        baudUcco.IsWmcActive = true;
        baudUcco.WmcStockAvailable = true;
        baudUcco.WmcInitialLoadReady = true;
        baudUcco.Description = "Pão de grãos para o café da manhã.";
        var unavailableBaudUcco = Product("SKU-BAUDUCCO-NO-STOCK", baudUcco.Name, false);
        unavailableBaudUcco.WmcCode = baudUcco.WmcCode;
        unavailableBaudUcco.IsWmcActive = true;
        unavailableBaudUcco.WmcStockAvailable = true;
        unavailableBaudUcco.WmcInitialLoadReady = true;
        db.AddRange(representative, seller, customer, baudUcco);
        db.Products.Add(unavailableBaudUcco);
        await db.SaveChangesAsync();
        db.ProductInventories.AddRange(
            new ProductInventory { ProductId = baudUcco.Id, QuantityOnHand = 10 },
            new ProductInventory { ProductId = unavailableBaudUcco.Id, QuantityOnHand = 0 });
        await db.SaveChangesAsync();

        var service = new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db));
        foreach (var query in new[] { "b", "bim", "bau", "BAU", "WMC-bau", "graos clas", "sku-bauducco", "912345678", "0123456789012", "00012345678905" })
        {
            var catalog = await service.GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, query, page: 1);
            Assert.Equal(baudUcco.Id, Assert.Single(Assert.IsType<SellerCatalogViewModel>(catalog).Products).Id);
        }
    }

    [Fact]
    public async Task Catalog_search_filters_in_sql_before_paging_with_a_shallow_sqlite_expression()
    {
        var sqlCapture = new SqlCommandCaptureInterceptor();
        await using var db = await TestDbContextFactory.CreateAsync(sqlCapture);
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "66.666.666/0001-66", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
        db.AddRange(representative, seller, customer);
        db.Products.AddRange(Enumerable.Range(1, 26).Select(index =>
        {
            var product = Product($"SKU-{index:000}", $"Pão de Café {index:000}", true);
            product.Brand = "BIMBO";
            return product;
        }));
        await db.SaveChangesAsync();

        var result = await new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db))
            .GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, "CAFE", page: 2, pageSize: 20);

        var catalog = Assert.IsType<SellerCatalogViewModel>(result);
        Assert.Equal(26, catalog.TotalCount);
        Assert.Equal(6, catalog.Products.Count);
        var searchCommands = sqlCapture.Commands
            .Where(command => command.Contains("translate", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(searchCommands);
        Assert.Contains(searchCommands, command => command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
        Assert.All(searchCommands, command =>
        {
            Assert.True(command.Contains("COUNT", StringComparison.OrdinalIgnoreCase)
                || command.Contains("LIMIT", StringComparison.OrdinalIgnoreCase), command);
            Assert.InRange(command.Split("replace(", StringSplitOptions.None).Length - 1, 0, 24);
        });
    }

    [Fact]
    public void Catalog_search_predicate_generates_postgresql_translate_sql_without_a_database_connection()
    {
        var options = new DbContextOptionsBuilder<Orofoods.Web.Data.ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused")
            .Options;
        using var db = new Orofoods.Web.Data.ApplicationDbContext(options);

        var sql = db.Products
            .Where(SellerCatalogService.CreateProductSearchPredicate("cafe"))
            .ToQueryString();

        Assert.Contains("translate(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Brand\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"WmcCode\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"Ean\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_and_selectable_ids_exclude_products_without_a_positive_base_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Vendedor", IsActive = true };
        var seller = new ApplicationUser { Id = "seller", UserName = "seller", IsActive = true, SalesRepresentative = representative };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "77.777.777/0001-77", Status = CustomerStatus.Approved, IsActive = true, SalesRepresentative = representative };
        var valid = Product("VALID", "Preço válido", true);
        var zero = Product("ZERO", "Preço zero", true);
        zero.BasePrice = 0m;
        var negative = Product("NEGATIVE", "Preço negativo", true);
        negative.BasePrice = -1m;
        db.AddRange(representative, seller, customer, valid, zero, negative);
        await db.SaveChangesAsync();

        var service = new SellerCatalogService(db, new SalesRepresentativeAccessService(db), new PriceService(db));
        var catalog = await service.GetCatalogAsync(SellerPrincipal(seller.Id), customer.Id, null);
        var selectableIds = await service.GetSelectableProductIdsAsync(
            SellerPrincipal(seller.Id), customer.Id, [valid.Id, zero.Id, negative.Id]);

        Assert.Equal(valid.Id, Assert.Single(Assert.IsType<SellerCatalogViewModel>(catalog).Products).Id);
        Assert.Equal([valid.Id], Assert.IsAssignableFrom<IReadOnlySet<int>>(selectableIds));
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

    private sealed class SqlCommandCaptureInterceptor : DbCommandInterceptor
    {
        public ConcurrentBag<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
