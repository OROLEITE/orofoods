using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcManualExportServiceTests
{
    [Fact]
    public async Task CanManuallyExport_matches_server_order_eligibility()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);

        Assert.True(service.CanManuallyExport(order));

        order.PaymentTerm!.Code = "UNSUPPORTED";
        Assert.False(service.CanManuallyExport(order));

        order.PaymentTerm.Code = "PIX";
        order.Items.Single().Product!.WmcCode = null;
        Assert.False(service.CanManuallyExport(order));

        order.Items.Single().Product!.WmcCode = "610601552";
        order.Status = OrderStatus.Received;
        Assert.False(service.CanManuallyExport(order));
    }

    [Theory]
    [InlineData(WmcOrderExportStatus.Processing)]
    [InlineData(WmcOrderExportStatus.Exported)]
    [InlineData(WmcOrderExportStatus.Imported)]
    public async Task CanManuallyExport_blocks_non_sendable_export_states(WmcOrderExportStatus status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.WmcOrderExportState = new WmcOrderExportState { OrderId = order.Id, Status = status };

        Assert.False(CreateService(db).CanManuallyExport(order));
    }

    [Fact]
    public async Task CanManuallyExport_blocks_history_requiring_manual_review()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.WmcOrderExportState = new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Pending,
            RequiresManualReview = true
        };

        Assert.False(CreateService(db).CanManuallyExport(order));
    }

    [Fact]
    public async Task Exports_approved_order_once_and_records_actor_file_and_result()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);

        var first = await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");
        var second = await service.ExportAsync(order.Id, "admin-2", "other@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, first.Disposition);
        Assert.Equal("WMC_ORO2026000801.txt", first.FileName);
        Assert.Contains("021  5  1  CD 000", first.Content);
        Assert.Equal(WmcManualExportDisposition.Duplicate, second.Disposition);
        var audit = await db.WmcExportAudits.SingleAsync(x => x.Succeeded);
        Assert.Equal(order.Id, audit.OrderId);
        Assert.Equal("admin-1", audit.ExportedByUserId);
        Assert.Equal("admin@orofoods.local", audit.ExportedByEmail);
        Assert.Equal(first.FileName, audit.FileName);
        Assert.True(audit.ExportedAt <= DateTime.UtcNow);
        Assert.Equal(WmcOrderExportStatus.Exported, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rejects_unapproved_order_and_records_failure_without_returning_a_file()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db, OrderStatus.Received);

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Null(result.Content);
        Assert.Contains("aprovado", result.Error, StringComparison.OrdinalIgnoreCase);
        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.False(audit.Succeeded);
        Assert.Equal("WMC_ORO2026000801.txt", audit.FileName);
        Assert.Equal(WmcOrderExportStatus.Error, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rejects_unsupported_payment_term_without_returning_a_file()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.PaymentTerm!.Code = "UNSUPPORTED";
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Null(result.Content);
        Assert.Contains("WMC_PAYMENT_TERM_UNSUPPORTED", result.Error);
        Assert.False((await db.WmcExportAudits.SingleAsync()).Succeeded);
    }

    [Fact]
    public async Task Rejects_incomplete_item_data_before_building_the_txt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.Items.Single().ProductNameSnapshot = "";
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Null(result.Content);
        Assert.Contains("itens completos", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False((await db.WmcExportAudits.SingleAsync()).Succeeded);
    }

    [Fact]
    public async Task Leaves_legacy_success_audits_unconfirmed_until_an_admin_resolves_them()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            ExportedByUserId = "old-admin",
            ExportedByEmail = "old@orofoods.local",
            ExportedAt = DateTime.UtcNow.AddDays(-1),
            FileName = "WMC_ORO2026000801.txt",
            Succeeded = true
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.ReviewRequired, result.Disposition);
        Assert.Equal(1, await db.WmcExportAudits.CountAsync());
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Pending, state.Status);
        Assert.True(state.RequiresManualReview);
    }

    [Fact]
    public async Task Explicitly_confirming_legacy_export_does_not_confirm_import()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            ExportedAt = DateTime.UtcNow.AddDays(-1),
            FileName = "WMC_ORO2026000801.txt",
            Succeeded = true
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var confirmation = await service.ConfirmPriorExportAsync(order.Id, "reviewer", "reviewer@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, confirmation.Disposition);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Exported, state.Status);
        Assert.False(state.RequiresManualReview);
        Assert.Equal("reviewer", state.ReviewedByUserId);
        Assert.Equal("reviewer", state.ExportedByUserId);
        Assert.Null(state.ImportedAt);
        Assert.Single(await db.WmcExportAudits.ToListAsync());
    }

    [Fact]
    public async Task Explicitly_clearing_legacy_uncertainty_allows_only_a_new_manual_export()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            ExportedAt = DateTime.UtcNow.AddDays(-1),
            FileName = "WMC_ORO2026000801.txt",
            Succeeded = false,
            Error = "Falha antiga"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var review = await service.ConfirmPriorExportDidNotReachWmcAsync(order.Id, "reviewer", "reviewer@orofoods.local");
        var export = await service.ExportAsync(order.Id, "admin-2", "admin2@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, review.Disposition);
        Assert.Equal(WmcManualExportDisposition.Exported, export.Disposition);
        Assert.Equal(2, await db.WmcExportAudits.CountAsync());
        Assert.Single(await db.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
        Assert.Equal(WmcOrderExportStatus.Exported, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Import_requires_a_separate_explicit_confirmation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var confirmation = await service.ConfirmImportedAsync(order.Id, "admin-2", "importer@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Imported, confirmation.Disposition);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Imported, state.Status);
        Assert.Equal("admin-2", state.ImportedByUserId);
        Assert.Equal("importer@orofoods.local", state.ImportedByEmail);
        Assert.NotNull(state.ImportedAt);
        Assert.Single(await db.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
    }

    [Fact]
    public async Task Stale_processing_is_recovered_as_error_without_automatic_export()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcOrderExportStates.Add(new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-30),
            Version = 3
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.RecoveredInterrupted, result.Disposition);
        Assert.Null(result.Content);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Error, state.Status);
        Assert.True(state.RequiresManualReview);
        Assert.Empty(await db.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
        Assert.Single(await db.WmcExportAudits.Where(x => !x.Succeeded).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_contexts_cannot_create_two_exports_for_one_order()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"wmc-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await AddEligibleOrderAsync(setup);
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var orderId = await firstDb.Orders.Select(x => x.Id).SingleAsync();
        var first = CreateService(firstDb).ExportAsync(orderId, "admin-1", "one@orofoods.local");
        var second = CreateService(secondDb).ExportAsync(orderId, "admin-2", "two@orofoods.local");

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, x => x.Disposition == WmcManualExportDisposition.Exported);
        Assert.Single(results, x => x.Disposition is WmcManualExportDisposition.Duplicate or WmcManualExportDisposition.Processing);
        Assert.Single(await firstDb.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
        Assert.Equal(WmcOrderExportStatus.Exported, (await firstDb.WmcOrderExportStates.SingleAsync()).Status);
    }

    private static WmcManualExportService CreateService(ApplicationDbContext db) =>
        new(db, new WmcOrderFileGenerator(), new WmcExportAuditService(db));

    private static async Task<Order> AddEligibleOrderAsync(ApplicationDbContext db, OrderStatus status = OrderStatus.Approved)
    {
        var creator = TestDbContextFactory.CreateOrderCreator("order-creator");
        var order = new Order
        {
            Number = "ORO-2026-000801",
            Status = status,
            CreatedAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" },
            CreatedByUser = creator,
            PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 },
            Total = 63.20m,
            Subtotal = 63.20m,
            Items =
            [
                new OrderItem
                {
                    Product = new Product
                    {
                        Name = "Produto",
                        Sku = "SKU-1",
                        WmcCode = "610601552",
                        Unit = "UN",
                        ProductCategory = new ProductCategory { Name = "Categoria", Slug = "wmc-test-category" }
                    },
                    ProductNameSnapshot = "Produto",
                    SkuSnapshot = "SKU-1",
                    Quantity = 1,
                    UnitPrice = 63.20m,
                    Subtotal = 63.20m
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
