using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class OrderIntegrationServiceTests
{
    [Fact]
    public async Task Loads_customer_and_products_before_sending_the_order_to_the_erp()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var category = new ProductCategory { Name = "Paes", Slug = "paes", IsActive = true };
        var product = new Product { Sku = "BIM-001", WmcCode = "610601552", Name = "Pao", ProductCategory = category, Brand = "BIMBO", Unit = "caixa", BasePrice = 50m };
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@orofoods.local", NormalizedUserName = "USER@OROFOODS.LOCAL", Email = "user@orofoods.local", NormalizedEmail = "USER@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000777", Customer = customer, CreatedByUser = user, Total = 100m, Status = OrderStatus.Approved };
        order.Items.Add(new OrderItem { Product = product, ProductNameSnapshot = "Pao", SkuSnapshot = "BIM-001", Quantity = 2, UnitPrice = 50m, Subtotal = 100m });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.True(erp.ReceivedMappedOrder);
        Assert.NotNull(erp.AttemptId);
        Assert.NotEqual(Guid.Empty, erp.AttemptId);
        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal("WMC_ORO2026000777.txt", persisted.ExternalOrderId);
    }

    [Fact]
    public async Task Marks_the_order_as_failed_when_the_erp_adapter_throws()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-2", UserName = "user2@orofoods.local", NormalizedUserName = "USER2@OROFOODS.LOCAL", Email = "user2@orofoods.local", NormalizedEmail = "USER2@OROFOODS.LOCAL" };
        var order = new Order { Number = "ORO-2026-000778", Customer = customer, CreatedByUser = user, Status = OrderStatus.Approved };
        db.Add(order);
        await db.SaveChangesAsync();

        await new OrderIntegrationService(db, new ThrowingErpOrderIntegration()).SendAsync(order.Id);

        var persisted = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha inesperada ao enviar o pedido ao ERP.", persisted.IntegrationError);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Received)]
    [InlineData(OrderStatus.UnderReview)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Picking)]
    [InlineData(OrderStatus.Invoiced)]
    [InlineData(OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Does_not_start_or_consume_an_attempt_for_an_order_that_is_not_approved(OrderStatus status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-ineligible", UserName = "ineligible@orofoods.local", NormalizedUserName = "INELIGIBLE@OROFOODS.LOCAL", Email = "ineligible@orofoods.local", NormalizedEmail = "INELIGIBLE@OROFOODS.LOCAL" };
        var order = new Order
        {
            Number = $"ORO-2026-{(int)status:000000}",
            Customer = customer,
            CreatedByUser = user,
            Status = status,
            IntegrationError = "erro anterior"
        };
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CapturingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.Equal(0, erp.SendCount);
        Assert.Equal(IntegrationStatus.Pending, order.IntegrationStatus);
        Assert.Null(order.LastIntegrationAttempt);
        Assert.Equal("erro anterior", order.IntegrationError);
    }

    [Fact]
    public async Task Direct_send_does_not_change_an_order_already_imported_manually_in_wmc()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var attemptedAt = DateTime.UtcNow.AddMinutes(-10);
        var importedAt = DateTime.UtcNow.AddMinutes(-5);
        var order = CreateOrderWithWmcState(
            "ORO-2026-000779",
            IntegrationStatus.Failed,
            WmcOrderExportStatus.Imported);
        order.IntegrationError = "Falha anterior do ERP";
        order.LastIntegrationAttempt = attemptedAt;
        order.ExternalOrderId = "WMC_ORO2026000779.txt";
        order.ErpOrderNumber = "ERP-779";
        order.WmcOrderExportState!.ImportedAt = importedAt;
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            Outcome = WmcExportOutcome.Imported,
            Succeeded = true,
            FileName = "WMC_ORO2026000779.txt",
            ExportedAt = importedAt
        });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.Equal(0, erp.SendCount);
        var persisted = await db.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == order.Id);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha anterior do ERP", persisted.IntegrationError);
        Assert.Equal(attemptedAt, persisted.LastIntegrationAttempt);
        Assert.Equal("WMC_ORO2026000779.txt", persisted.ExternalOrderId);
        Assert.Equal("ERP-779", persisted.ErpOrderNumber);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.Equal(importedAt, persisted.WmcOrderExportState.ImportedAt);
        var audit = await db.WmcExportAudits.SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal(WmcExportOutcome.Imported, audit.Outcome);
        Assert.Equal("WMC_ORO2026000779.txt", audit.FileName);
    }

    [Fact]
    public async Task Concurrent_direct_sends_do_not_reprocess_an_imported_wmc_order()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"order-integration-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = CreateOrderWithWmcState("ORO-2026-000780", IntegrationStatus.Failed, WmcOrderExportStatus.Imported);
            order.IntegrationError = "Falha histórica";
            order.LastIntegrationAttempt = DateTime.UtcNow.AddMinutes(-3);
            order.WmcExportAudits.Add(new WmcExportAudit
            {
                Outcome = WmcExportOutcome.Imported,
                Succeeded = true,
                FileName = "WMC_ORO2026000780.txt"
            });
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var erp = new CountingErpOrderIntegration();

        await Task.WhenAll(
            new OrderIntegrationService(firstDb, erp).SendAsync(orderId),
            new OrderIntegrationService(secondDb, erp).SendAsync(orderId));

        Assert.Equal(0, erp.SendCount);
        await using var verification = new ApplicationDbContext(options);
        var persisted = await verification.Orders.Include(item => item.WmcOrderExportState).SingleAsync(item => item.Id == orderId);
        Assert.Equal(IntegrationStatus.Failed, persisted.IntegrationStatus);
        Assert.Equal("Falha histórica", persisted.IntegrationError);
        Assert.NotNull(persisted.LastIntegrationAttempt);
        Assert.Equal(WmcOrderExportStatus.Imported, persisted.WmcOrderExportState!.Status);
        Assert.Equal("WMC_ORO2026000780.txt", await verification.WmcExportAudits
            .Where(item => item.OrderId == orderId)
            .Select(item => item.FileName)
            .SingleAsync());
    }

    [Fact]
    public async Task Reprocessing_remains_available_before_manual_wmc_import_is_confirmed()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState(
            "ORO-2026-000781",
            IntegrationStatus.Pending,
            WmcOrderExportStatus.Exported);
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        await new OrderIntegrationService(db, erp).SendAsync(order.Id);

        Assert.Equal(1, erp.SendCount);
        Assert.Equal(IntegrationStatus.Succeeded, order.IntegrationStatus);
        Assert.Equal(WmcOrderExportStatus.Exported, order.WmcOrderExportState!.Status);
    }

    [Fact]
    public async Task Selected_send_returns_result_and_blocks_a_second_json_attempt_after_success()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState("ORO-2026-000782", IntegrationStatus.Pending, WmcOrderExportStatus.Pending);
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();
        var service = new OrderIntegrationService(db, erp);

        var first = await service.SendSelectedAsync(order.Id, erp);
        var second = await service.SendSelectedAsync(order.Id, erp);

        Assert.True(first?.Succeeded);
        Assert.Null(second);
        Assert.Equal(1, erp.SendCount);
    }

    [Theory]
    [InlineData(WmcExportOutcome.Started)]
    [InlineData(WmcExportOutcome.Unknown)]
    [InlineData(WmcExportOutcome.Succeeded)]
    public async Task Selected_send_does_not_reclaim_json_attempts_that_are_active_uncertain_or_succeeded(WmcExportOutcome outcome)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState($"ORO-2026-{(int)outcome:000000}", IntegrationStatus.Pending, WmcOrderExportStatus.Exported);
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            AttemptId = Guid.NewGuid(),
            Source = WmcExportSource.Automatic,
            Outcome = outcome,
            Succeeded = outcome == WmcExportOutcome.Succeeded,
            ExportedAt = DateTime.UtcNow
        });
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        var result = await new OrderIntegrationService(db, erp).SendSelectedAsync(order.Id, erp);

        Assert.Null(result);
        Assert.Equal(0, erp.SendCount);
    }

    [Fact]
    public async Task Selected_send_does_not_reclaim_an_order_with_a_generated_txt_artifact()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrderWithWmcState("ORO-2026-000783", IntegrationStatus.Pending, WmcOrderExportStatus.Exported);
        db.Add(order);
        await db.SaveChangesAsync();
        var erp = new CountingErpOrderIntegration();

        var result = await new OrderIntegrationService(db, erp).SendSelectedAsync(order.Id, erp);

        Assert.Null(result);
        Assert.Equal(0, erp.SendCount);
    }

    [Fact]
    public async Task Concurrent_selected_sends_reserve_the_order_for_only_one_transmission()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"selected-order-import-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = CreateOrderWithWmcState("ORO-2026-000784", IntegrationStatus.Pending, WmcOrderExportStatus.Pending);
            setup.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var erp = new CountingErpOrderIntegration();
        var results = await Task.WhenAll(
            new OrderIntegrationService(firstDb, erp).SendSelectedAsync(orderId, erp),
            new OrderIntegrationService(secondDb, erp).SendSelectedAsync(orderId, erp));

        Assert.Single(results, result => result?.Succeeded == true);
        Assert.Single(results, result => result is null);
        Assert.Equal(1, erp.SendCount);
    }

    private static Order CreateOrderWithWmcState(
        string orderNumber,
        IntegrationStatus integrationStatus,
        WmcOrderExportStatus wmcStatus)
    {
        var userId = $"creator-{Guid.NewGuid():N}";
        return new Order
        {
            Number = orderNumber,
            Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" },
            CreatedByUser = new ApplicationUser
            {
                Id = userId,
                UserName = $"{userId}@test.local",
                NormalizedUserName = $"{userId}@TEST.LOCAL",
                Email = $"{userId}@test.local",
                NormalizedEmail = $"{userId}@TEST.LOCAL"
            },
            Status = OrderStatus.Approved,
            IntegrationStatus = integrationStatus,
            WmcOrderExportState = new WmcOrderExportState { Status = wmcStatus }
        };
    }

    private sealed class CountingErpOrderIntegration : IErpOrderIntegration
    {
        private int sendCount;
        public int SendCount => Volatile.Read(ref sendCount);

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null)
        {
            Interlocked.Increment(ref sendCount);
            return Task.FromResult(new ErpOrderResult(true, $"ERP-{order.Number}"));
        }
    }

    private sealed class CapturingErpOrderIntegration : IErpOrderIntegration
    {
        public bool ReceivedMappedOrder { get; private set; }
        public int SendCount { get; private set; }
        public Guid? AttemptId { get; private set; }

        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null)
        {
            SendCount++;
            AttemptId = attemptId;
            ReceivedMappedOrder = order.Customer?.WmcCode == "107072" && order.Items.Single().Product?.WmcCode == "610601552";
            return Task.FromResult(new ErpOrderResult(true, "WMC_ORO2026000777.txt"));
        }
    }

    private sealed class ThrowingErpOrderIntegration : IErpOrderIntegration
    {
        public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null) =>
            throw new IOException("Diretorio indisponivel.");
    }
}
