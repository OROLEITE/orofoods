using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WmcSyncServiceTests
{
    [Fact]
    public async Task New_customer_is_created_pending_with_a_placeholder_cnpj()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, customers: [new WmcCustomerRecord("C001", "Padaria Teste")]);

        var result = await service.SyncCustomersAsync();

        Assert.Equal(1, result.RecordsRead);
        Assert.Equal(1, result.RecordsCreated);
        Assert.Null(result.ErrorMessage);
        var customer = Assert.Single(db.Customers);
        Assert.Equal("C001", customer.WmcCode);
        Assert.Equal("Padaria Teste", customer.TradeName);
        Assert.Equal(CustomerStatus.Pending, customer.Status);
        Assert.StartsWith("WMC-", customer.Cnpj);
    }

    [Fact]
    public async Task Existing_customer_is_updated_and_not_duplicated_on_repeated_sync()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer { WmcCode = "C002", LegalName = "Old", TradeName = "Old", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved });
        await db.SaveChangesAsync();
        var service = CreateService(db, customers: [new WmcCustomerRecord("C002", "Nome Novo")]);

        var first = await service.SyncCustomersAsync();
        var second = await service.SyncCustomersAsync();

        Assert.Equal(1, first.RecordsUpdated);
        Assert.Equal(1, second.RecordsSkipped);
        Assert.Equal(0, second.RecordsCreated);
        Assert.Single(db.Customers);
        Assert.Equal("Nome Novo", db.Customers.Single().TradeName);
        Assert.Equal("Approved", db.Customers.Single().Status.ToString());
    }

    [Fact]
    public async Task New_product_is_created_inactive_in_a_pending_category_without_guessing_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, products: [new WmcProductRecord("P001", "Pao Teste", "A", "CX", 10, 12)]);

        var result = await service.SyncProductsAsync();

        Assert.Equal(1, result.RecordsCreated);
        var product = Assert.Single(db.Products);
        Assert.Equal("P001", product.WmcCode);
        Assert.Equal("Pao Teste", product.Name);
        Assert.False(product.IsActive);
        Assert.False(product.IsAvailable);
        Assert.Equal(0m, product.BasePrice);
        var category = db.ProductCategories.Single(c => c.Id == product.ProductCategoryId);
        Assert.Equal("wmc-pendente-categorizacao", category.Slug);
    }

    [Fact]
    public async Task Existing_product_is_updated_and_not_duplicated_on_repeated_sync()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Congelados", Slug = "congelados", IsActive = true };
        db.Add(category);
        await db.SaveChangesAsync();
        db.Products.Add(new Product { WmcCode = "P002", Sku = "SKU-1", Name = "Nome Antigo", ProductCategoryId = category.Id, Unit = "caixa" });
        await db.SaveChangesAsync();
        var service = CreateService(db, products: [new WmcProductRecord("P002", "Nome Atualizado", "A", "CX", 5, 5)]);

        var first = await service.SyncProductsAsync();
        var second = await service.SyncProductsAsync();

        Assert.Equal(1, first.RecordsUpdated);
        Assert.Equal(1, second.RecordsSkipped);
        Assert.Single(db.Products);
        Assert.Equal("Nome Atualizado", db.Products.Single().Name);
    }

    [Fact]
    public async Task Stock_updates_only_products_already_linked_and_skips_the_rest()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Congelados", Slug = "congelados", IsActive = true };
        db.Add(category);
        await db.SaveChangesAsync();
        var linked = new Product { WmcCode = "P003", Sku = "SKU-2", Name = "Linked", ProductCategoryId = category.Id, Unit = "caixa" };
        db.Products.Add(linked);
        await db.SaveChangesAsync();
        var service = CreateService(db, products:
        [
            new WmcProductRecord("P003", "Linked", "A", "CX", 40, 40),
            new WmcProductRecord("P999", "Unlinked", "A", "CX", 10, 10)
        ]);

        var result = await service.SyncStockAsync();

        Assert.Equal(1, result.RecordsCreated);
        Assert.Equal(1, result.RecordsSkipped);
        var inventory = db.ProductInventories.Single(i => i.ProductId == linked.Id);
        Assert.Equal(40, inventory.QuantityOnHand);
    }

    [Fact]
    public async Task Seller_sync_is_a_safe_no_op_until_the_real_wmc_table_is_confirmed()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db);

        var result = await service.SyncSellersAsync();

        Assert.Equal(0, result.RecordsRead);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Firebird_failure_does_not_delete_or_change_existing_customers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer { WmcCode = "C010", LegalName = "Existing", TradeName = "Existing", Cnpj = "22.222.222/0001-22", Status = CustomerStatus.Approved });
        await db.SaveChangesAsync();
        var service = CreateService(db, customerReader: new FailingCustomerReader());

        var result = await service.SyncCustomersAsync();

        Assert.NotNull(result.ErrorMessage);
        var customer = Assert.Single(db.Customers);
        Assert.Equal("Existing", customer.TradeName);
    }

    [Fact]
    public async Task Healthy_connection_reports_healthy()
    {
        var check = new WmcFirebirdHealthCheck(new FakeReader(canConnect: true), Options.Create(new WmcFirebirdOptions { Enabled = true }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Unreachable_firebird_reports_unhealthy()
    {
        var check = new WmcFirebirdHealthCheck(new FakeReader(canConnect: false), Options.Create(new WmcFirebirdOptions { Enabled = true }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Concurrent_sync_is_blocked_while_one_is_already_running()
    {
        var coordinator = new WmcSyncCoordinator();
        var gate = new TaskCompletionSource();

        var firstTask = coordinator.RunExclusivelyAsync(async () =>
        {
            await gate.Task;
            return new WmcSyncRunResult(DateTime.UtcNow, DateTime.UtcNow, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty);
        });

        var secondResult = await coordinator.RunExclusivelyAsync(() => Task.FromResult(
            new WmcSyncRunResult(DateTime.UtcNow, DateTime.UtcNow, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty, WmcSyncEntityResult.Empty)));

        Assert.Null(secondResult);
        gate.SetResult();
        await firstTask;
    }

    [Fact]
    public void Firebird_reader_exposes_only_query_and_connectivity_methods_never_a_write_path()
    {
        var methodNames = typeof(IWmcFirebirdReader).GetMethods().Select(m => m.Name).ToList();

        Assert.Equal(["CanConnectAsync", "QueryAsync"], methodNames.OrderBy(x => x));
        Assert.DoesNotContain(methodNames, name => name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Wmc_integration_source_never_logs_the_firebird_password_or_touches_mercado_pago()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var wmcDirectory = Path.Combine(projectRoot, "Orofoods.Web", "Integrations", "Erp", "Wmc");
        foreach (var file in Directory.GetFiles(wmcDirectory, "*.cs"))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("MercadoPago", source, StringComparison.OrdinalIgnoreCase);
            foreach (var line in source.Split('\n').Where(l => l.Contains("Log")))
            {
                Assert.DoesNotContain("Password", line, StringComparison.Ordinal);
            }
        }
    }

    private static WmcSyncService CreateService(
        Orofoods.Web.Data.ApplicationDbContext db,
        IReadOnlyList<WmcCustomerRecord>? customers = null,
        IReadOnlyList<WmcProductRecord>? products = null,
        IWmcCustomerReader? customerReader = null,
        IWmcProductReader? productReader = null) =>
        new(
            db,
            customerReader ?? new FakeCustomerReader(customers ?? []),
            productReader ?? new FakeProductReader(products ?? []),
            new UndiscoveredWmcSellerReader(NullLogger<UndiscoveredWmcSellerReader>.Instance),
            NullLogger<WmcSyncService>.Instance,
            TimeProvider.System);

    private sealed class FakeCustomerReader(IReadOnlyList<WmcCustomerRecord> rows) : IWmcCustomerReader
    {
        public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
    }

    private sealed class FailingCustomerReader : IWmcCustomerReader
    {
        public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror indisponivel.");
    }

    private sealed class FakeProductReader(IReadOnlyList<WmcProductRecord> rows) : IWmcProductReader
    {
        public Task<IReadOnlyList<WmcProductRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
    }

    private sealed class FakeReader(bool canConnect) : IWmcFirebirdReader
    {
        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<System.Data.Common.DbDataReader, T> map, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(canConnect);
    }
}
