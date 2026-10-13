using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class WmcInitialLoadReadinessReleaseServiceTests
{
    [Fact]
    public void PostgreSql_release_audit_migration_is_registered()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=design_time;Username=design_time")
            .Options;
        using var db = new PostgreSqlApplicationDbContext(options);

        Assert.Contains("20261013160000_AddWmcInitialLoadReleaseAudit", db.Database.GetMigrations());
    }

    [Fact]
    public void PostgreSql_release_audit_migration_generates_expected_ddl_without_connecting()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=design_time;Username=design_time")
            .Options;
        using var db = new PostgreSqlApplicationDbContext(options);
        var migrator = db.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            "20261012160000_AddManualInventoryOverride",
            "20261013160000_AddWmcInitialLoadReleaseAudit");

        Assert.Contains("CREATE TABLE \"WmcInitialLoadReleaseAudits\"", script, StringComparison.Ordinal);
        Assert.Contains("\"AdminUserId\" character varying(450) NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("\"ReleasedAtUtc\" timestamp with time zone NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("\"Reason\" character varying(300) NOT NULL", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feature_is_disabled_by_default()
    {
        Assert.False(new WmcInitialLoadReleaseOptions().Enabled);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", false)]
    public async Task Release_is_blocked_outside_staging_or_without_explicit_configuration(string environmentName, bool enabled)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddEligibleProductAsync(db);
        var service = CreateService(db, environmentName, enabled);

        var result = await service.ReleaseAsync(product.Id, "Homologação", "admin-1");

        Assert.Equal(WmcInitialLoadReleaseStatus.Disabled, result.Status);
        Assert.False((await db.Products.SingleAsync()).WmcInitialLoadReady);
        Assert.Empty(await db.WmcInitialLoadReleaseAudits.ToListAsync());
    }

    [Theory]
    [InlineData(false, true, "O produto está inativo no WMC.")]
    [InlineData(true, false, "O indicador de estoque WMC está indisponível.")]
    public async Task Release_refuses_false_wmc_protection_flags(bool isWmcActive, bool wmcStockAvailable, string expectedMessage)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddEligibleProductAsync(db);
        product.IsWmcActive = isWmcActive;
        product.WmcStockAvailable = wmcStockAvailable;
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.ReleaseAsync(product.Id, "Contagem física validada", "admin-1");

        Assert.Equal(WmcInitialLoadReleaseStatus.Blocked, result.Status);
        Assert.Equal(expectedMessage, result.Message);
        Assert.False(product.WmcInitialLoadReady);
        Assert.Empty(await db.WmcInitialLoadReleaseAudits.ToListAsync());
    }

    [Fact]
    public async Task Release_is_limited_to_wmc_code_243680_and_manual_inventory_with_minimum_available()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddEligibleProductAsync(db, wmcCode: "999999");
        var service = CreateService(db);

        var result = await service.ReleaseAsync(product.Id, "Homologação", "admin-1");

        Assert.Equal(WmcInitialLoadReleaseStatus.NotFound, result.Status);
        Assert.False(product.WmcInitialLoadReady);
        Assert.Empty(await db.WmcInitialLoadReleaseAudits.ToListAsync());
    }

    [Theory]
    [InlineData(false, true, 1, 0, 1)]
    [InlineData(true, false, 1, 0, 1)]
    [InlineData(true, true, 1, 1, 1)]
    [InlineData(true, true, 1, 0, 2)]
    public async Task Release_preserves_other_availability_gates(bool isActive, bool isAvailable, int onHand, int reserved, int minimumCases)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddEligibleProductAsync(db, isActive: isActive, isAvailable: isAvailable, onHand: onHand, reserved: reserved, minimumCases: minimumCases);
        var service = CreateService(db);

        var result = await service.ReleaseAsync(product.Id, "Homologação", "admin-1");

        Assert.Equal(WmcInitialLoadReleaseStatus.Blocked, result.Status);
        Assert.False(product.WmcInitialLoadReady);
        Assert.Equal(isActive, product.IsActive);
        Assert.Equal(isAvailable, product.IsAvailable);
        var inventory = await db.ProductInventories.SingleAsync();
        Assert.Equal(onHand, inventory.QuantityOnHand);
        Assert.Equal(reserved, inventory.QuantityReserved);
        Assert.True(inventory.IsManuallyAdjusted);
        Assert.Empty(await db.WmcInitialLoadReleaseAudits.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("reason-over-300")]
    public async Task Release_requires_a_valid_reason(string? reason)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddEligibleProductAsync(db);
        var service = CreateService(db);
        if (reason == "reason-over-300") reason = new string('x', 301);

        var result = await service.ReleaseAsync(product.Id, reason, "admin-1");

        Assert.Equal(WmcInitialLoadReleaseStatus.Blocked, result.Status);
        Assert.False(product.WmcInitialLoadReady);
        Assert.Empty(await db.WmcInitialLoadReleaseAudits.ToListAsync());
    }

    [Fact]
    public async Task Successful_release_changes_only_readiness_and_persists_actor_product_utc_time_and_reason()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = await AddEligibleProductAsync(db);
        var inventory = await db.ProductInventories.SingleAsync();
        var releasedAt = new DateTimeOffset(2026, 10, 13, 14, 30, 0, TimeSpan.Zero);
        var service = CreateService(db, now: releasedAt);

        var result = await service.ReleaseAsync(product.Id, "Contagem física conferida", "admin-42");

        Assert.Equal(WmcInitialLoadReleaseStatus.Released, result.Status);
        await db.Entry(product).ReloadAsync();
        Assert.True(product.WmcInitialLoadReady);
        Assert.True(product.IsActive);
        Assert.True(product.IsAvailable);
        Assert.True(product.IsWmcActive);
        Assert.True(product.WmcStockAvailable);
        Assert.Equal(1, product.MinimumCases);
        Assert.Equal(1, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.QuantityReserved);
        Assert.True(inventory.IsManuallyAdjusted);
        var audit = Assert.Single(await db.WmcInitialLoadReleaseAudits.ToListAsync());
        Assert.Equal("admin-42", audit.AdminUserId);
        Assert.Equal(product.Id, audit.ProductId);
        Assert.Equal("243680", audit.WmcCode);
        Assert.Equal(releasedAt, audit.ReleasedAtUtc);
        Assert.Equal(TimeSpan.Zero, audit.ReleasedAtUtc.Offset);
        Assert.Equal("Contagem física conferida", audit.Reason);
    }

    [Fact]
    public async Task Concurrent_releases_create_one_transition_and_one_audit_record()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-readiness-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString())
            .Options;
        int productId;

        try
        {
            await using (var seedDb = new ApplicationDbContext(options))
            {
                await seedDb.Database.EnsureCreatedAsync();
                productId = (await AddEligibleProductAsync(seedDb)).Id;
            }

            await using var firstDb = new ApplicationDbContext(options);
            await using var secondDb = new ApplicationDbContext(options);
            var first = CreateService(firstDb);
            var second = CreateService(secondDb);

            var results = await Task.WhenAll(
                first.ReleaseAsync(productId, "Primeira liberação", "admin-1"),
                second.ReleaseAsync(productId, "Liberação concorrente", "admin-2"));

            Assert.Single(results, result => result.Status == WmcInitialLoadReleaseStatus.Released);
            Assert.Single(results, result => result.Status == WmcInitialLoadReleaseStatus.AlreadyReady);
            await using var verifyDb = new ApplicationDbContext(options);
            Assert.True((await verifyDb.Products.SingleAsync()).WmcInitialLoadReady);
            Assert.Single(await verifyDb.WmcInitialLoadReleaseAudits.ToListAsync());
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static async Task<Product> AddEligibleProductAsync(
        ApplicationDbContext db,
        string wmcCode = "243680",
        bool isActive = true,
        bool isAvailable = true,
        int onHand = 1,
        int reserved = 0,
        int minimumCases = 1)
    {
        var category = new ProductCategory { Name = "Teste", Slug = "teste", IsActive = true };
        db.ProductCategories.Add(category);
        var product = new Product
        {
            Sku = "WMC-243680",
            WmcCode = wmcCode,
            Name = "Produto homologação",
            ProductCategory = category,
            IsActive = isActive,
            IsAvailable = isAvailable,
            IsWmcActive = true,
            WmcStockAvailable = true,
            WmcInitialLoadReady = false,
            MinimumCases = minimumCases
        };
        db.Products.Add(product);
        db.ProductInventories.Add(new ProductInventory
        {
            Product = product,
            QuantityOnHand = onHand,
            QuantityReserved = reserved,
            IsManuallyAdjusted = true
        });
        await db.SaveChangesAsync();
        return product;
    }

    private static WmcInitialLoadReadinessReleaseService CreateService(
        ApplicationDbContext db,
        string environmentName = "Staging",
        bool enabled = true,
        DateTimeOffset? now = null) => new(
            db,
            new TestEnvironment(environmentName),
            Options.Create(new WmcInitialLoadReleaseOptions { Enabled = enabled }),
            new TestTimeProvider(now ?? DateTimeOffset.UtcNow));

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
