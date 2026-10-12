using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class AdminDashboardControllerTests
{
    [Fact]
    public void Dashboard_controller_has_an_unambiguous_mvc_activation_constructor()
    {
        var factory = ActivatorUtilities.CreateFactory(typeof(DashboardController), Type.EmptyTypes);

        Assert.NotNull(factory);
    }

    [Fact]
    public async Task Index_counts_failed_integrations_and_low_stock_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@test", Email = "user@test" };
        var product = new Product { Sku = "BIM-001", Name = "Pao", Brand = "Bimbo", ProductCategory = new ProductCategory { Name = "Congelados", Slug = "congelados" } };
        db.AddRange(customer, user,
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-1", IntegrationStatus = IntegrationStatus.Failed },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-PENDING", IntegrationStatus = IntegrationStatus.Pending },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-PROCESSING", IntegrationStatus = IntegrationStatus.Processing },
            new ProductInventory { Product = product, QuantityOnHand = 10, QuantityReserved = 1 });
        await db.SaveChangesAsync();
        var recoveredOrder = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-2", IntegrationStatus = IntegrationStatus.Succeeded };
        db.Orders.Add(recoveredOrder);
        await db.SaveChangesAsync();
        db.WmcExportAudits.AddRange(
            new WmcExportAudit { OrderId = recoveredOrder.Id, Succeeded = false, Error = "Codigo WMC ausente", ExportedAt = DateTime.UtcNow.AddMinutes(-2) },
            new WmcExportAudit { OrderId = recoveredOrder.Id, Succeeded = true, FileName = "WMC_ORO2.txt", ExportedAt = DateTime.UtcNow.AddMinutes(-1) },
            new WmcExportAudit { OrderId = 1, Succeeded = false, Error = "Codigo WMC ausente", ExportedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(1, model.FailedIntegrations);
        Assert.Equal(1, model.PendingIntegrations);
        Assert.Equal(1, model.ProcessingIntegrations);
        Assert.True(model.HasIntegrationRecords);
        Assert.Equal(1, model.LowStockProducts);
        Assert.Equal(1, model.FailedWmcExports);
    }

    [Fact]
    public async Task Index_compares_month_revenue_using_only_non_cancelled_orders_when_previous_month_has_data()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@test", Email = "user@test" };
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        db.AddRange(customer, user,
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-CURRENT", Total = 200m, CreatedAt = monthStart.AddDays(1), Status = OrderStatus.Received },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-PREVIOUS", Total = 100m, CreatedAt = monthStart.AddDays(-1), Status = OrderStatus.Received },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-CANCELLED", Total = 500m, CreatedAt = monthStart.AddDays(1), Status = OrderStatus.Cancelled });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(200m, model.MonthRevenue);
        Assert.Equal(100m, model.MonthRevenueChangePercent);
    }

    [Fact]
    public async Task Index_omits_revenue_comparison_when_previous_month_has_no_revenue()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@test", Email = "user@test" };
        db.AddRange(customer, user, new Order { Customer = customer, CreatedByUser = user, Number = "ORO-CURRENT", Total = 200m, CreatedAt = DateTime.UtcNow, Status = OrderStatus.Received });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Null(model.MonthRevenueChangePercent);
    }

    [Fact]
    public async Task Index_counts_today_operation_by_current_status_and_delivery_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var today = now.UtcDateTime.Date;
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@test", Email = "user@test" };
        var todayOrders = new[]
        {
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-RECEIVED", CreatedAt = today.AddHours(8), Status = OrderStatus.Received },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-REVIEW", CreatedAt = today.AddHours(9), Status = OrderStatus.UnderReview },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-APPROVED", CreatedAt = today.AddHours(10), Status = OrderStatus.Approved },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-PICKING", CreatedAt = today.AddHours(11), Status = OrderStatus.Picking },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-INVOICED", CreatedAt = today.AddHours(12), Status = OrderStatus.Invoiced },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-DELIVERY", CreatedAt = today.AddHours(13), Status = OrderStatus.OutForDelivery },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-DELIVERED", CreatedAt = today.AddHours(14), Status = OrderStatus.Delivered },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-TODAY-CANCELLED", CreatedAt = today.AddHours(15), Status = OrderStatus.Cancelled }
        };
        var olderDeliveredOrder = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-OLDER-DELIVERED", CreatedAt = today.AddDays(-1), Status = OrderStatus.Delivered };
        db.Orders.AddRange(todayOrders.Append(olderDeliveredOrder));
        await db.SaveChangesAsync();

        db.OrderStatusHistories.AddRange(
            new OrderStatusHistory { OrderId = todayOrders[6].Id, Status = OrderStatus.Delivered, ChangedAt = today.AddHours(14) },
            new OrderStatusHistory { OrderId = olderDeliveredOrder.Id, Status = OrderStatus.Delivered, ChangedAt = today.AddHours(16) },
            new OrderStatusHistory { OrderId = olderDeliveredOrder.Id, Status = OrderStatus.Delivered, ChangedAt = today.AddHours(17) },
            new OrderStatusHistory { OrderId = olderDeliveredOrder.Id, Status = OrderStatus.Delivered, ChangedAt = today.AddDays(-1).AddHours(18) });
        await db.SaveChangesAsync();

        var result = await CreateController(db, new FixedTimeProvider(now)).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(8, model.OrdersTodayTotal);
        Assert.Equal(2, model.OrdersTodayAwaitingAction);
        Assert.Equal(1, model.OrdersTodayReceived);
        Assert.Equal(3, model.OrdersTodayInAnalysisOrSeparation);
        Assert.Equal(2, model.OrdersTodayReadyOrInDelivery);
        Assert.Equal(1, model.OrdersTodayDelivered);
        Assert.Equal(2, model.OrdersDeliveredToday);
    }

    [Fact]
    public async Task Index_uses_sao_paulo_day_after_utc_midnight_before_local_midnight()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "timezone-user", UserName = "timezone@test", Email = "timezone@test" };
        db.AddRange(customer, user,
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-SP-TODAY", CreatedAt = new DateTime(2026, 8, 10, 0, 15, 0, DateTimeKind.Utc), Status = OrderStatus.Received },
            new Order { Customer = customer, CreatedByUser = user, Number = "ORO-SP-TOMORROW", CreatedAt = new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc), Status = OrderStatus.Received });
        await db.SaveChangesAsync();

        var result = await CreateController(db, new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 0, 30, 0, TimeSpan.Zero))).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(1, model.OrdersTodayTotal);
        Assert.Equal(1, model.OrdersTodayReceived);
    }

    [Fact]
    public async Task Index_uses_local_midnight_as_inclusive_start_and_exclusive_end_for_orders_and_deliveries()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "local-midnight-user", UserName = "midnight@test", Email = "midnight@test" };
        var beforeStart = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-BEFORE-START", CreatedAt = new DateTime(2026, 8, 10, 2, 59, 59, DateTimeKind.Utc), Status = OrderStatus.Delivered };
        var atStart = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-AT-START", CreatedAt = new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc), Status = OrderStatus.Received };
        var beforeEnd = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-BEFORE-END", CreatedAt = new DateTime(2026, 8, 11, 2, 59, 59, DateTimeKind.Utc), Status = OrderStatus.Delivered };
        var atEnd = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-AT-END", CreatedAt = new DateTime(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc), Status = OrderStatus.Delivered };
        db.AddRange(customer, user, beforeStart, atStart, beforeEnd, atEnd);
        await db.SaveChangesAsync();
        db.OrderStatusHistories.AddRange(
            new OrderStatusHistory { OrderId = beforeStart.Id, Status = OrderStatus.Delivered, ChangedAt = new DateTime(2026, 8, 10, 2, 59, 59, DateTimeKind.Utc) },
            new OrderStatusHistory { OrderId = beforeEnd.Id, Status = OrderStatus.Delivered, ChangedAt = new DateTime(2026, 8, 11, 2, 59, 59, DateTimeKind.Utc) },
            new OrderStatusHistory { OrderId = atEnd.Id, Status = OrderStatus.Delivered, ChangedAt = new DateTime(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateController(db, new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 3, 0, 0, TimeSpan.Zero))).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(2, model.OrdersTodayTotal);
        Assert.Equal(1, model.OrdersTodayDelivered);
        Assert.Equal(1, model.OrdersDeliveredToday);
    }

    [Fact]
    public async Task Index_reports_only_active_point_terminals_and_persisted_whatsapp_failures()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var conversation = new WhatsAppConversation { PhoneNumber = "5511999999999" };
        db.AddRange(
            new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "active-device", IsActive = true },
            new PaymentTerminal { Provider = PaymentTerminalProvider.MercadoPago, DeviceId = "inactive-device", IsActive = false },
            conversation);
        await db.SaveChangesAsync();
        db.WhatsAppMessages.AddRange(
            new WhatsAppMessage { ConversationId = conversation.Id, Direction = WhatsAppMessageDirection.Outbound, Type = WhatsAppMessageType.Text, Status = WhatsAppMessageStatus.Failed, FailedAt = DateTime.UtcNow },
            new WhatsAppMessage { ConversationId = conversation.Id, Direction = WhatsAppMessageDirection.Outbound, Type = WhatsAppMessageType.Text, Status = WhatsAppMessageStatus.Delivered, DeliveredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(1, model.ActiveMercadoPagoPointTerminals);
        Assert.Equal(1, model.FailedWhatsAppMessages);
    }

    [Fact]
    public async Task Index_works_without_point_terminal_or_whatsapp_message_records()
    {
        await using var db = await TestDbContextFactory.CreateAsync();

        var result = await CreateController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(0, model.OrdersTodayTotal);
        Assert.Equal(0, model.OrdersTodayAwaitingAction);
        Assert.Equal(0, model.OrdersTodayReceived);
        Assert.Equal(0, model.OrdersTodayInAnalysisOrSeparation);
        Assert.Equal(0, model.OrdersTodayReadyOrInDelivery);
        Assert.Equal(0, model.OrdersTodayDelivered);
        Assert.Equal(0, model.OrdersDeliveredToday);
        Assert.Equal(0, model.ActiveMercadoPagoPointTerminals);
        Assert.Equal(0, model.FailedWhatsAppMessages);
        Assert.False(model.WmcSyncIsRunning);
        Assert.False(model.WmcSyncHasRun);
        Assert.False(model.WmcSyncHasFailed);
        Assert.False(model.HasIntegrationRecords);
    }

    [Fact]
    public async Task Index_reads_the_existing_wmc_coordinator_failure_without_starting_a_sync()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var coordinator = new WmcSyncCoordinator();
        var lastRun = new WmcSyncRunResult(
            now.UtcDateTime.AddMinutes(-1),
            now.UtcDateTime,
            new WmcSyncEntityResult(1, 0, 0, 0, "Falha de leitura"),
            WmcSyncEntityResult.Empty,
            WmcSyncEntityResult.Empty,
            WmcSyncEntityResult.Empty);
        await coordinator.RunExclusivelyAsync(() => Task.FromResult(lastRun));

        var result = await CreateController(db, new FixedTimeProvider(now), coordinator).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.True(model.WmcSyncHasRun);
        Assert.True(model.WmcSyncHasFailed);
        Assert.False(model.WmcSyncIsRunning);
        Assert.Same(lastRun, coordinator.LastRun);
    }

    [Fact]
    public async Task Index_returns_real_recent_orders_newest_first_with_customer_data()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente Real", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "recent-orders-user", UserName = "recent-orders@test", Email = "recent-orders@test" };
        var older = new Order
        {
            Customer = customer,
            CreatedByUser = user,
            Number = "ORO-RECENT-OLDER",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Total = 25m,
            Status = OrderStatus.Received
        };
        var newer = new Order
        {
            Customer = customer,
            CreatedByUser = user,
            Number = "ORO-RECENT-NEWER",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Total = 50m,
            Status = OrderStatus.Approved
        };
        db.AddRange(customer, user, older, newer);
        await db.SaveChangesAsync();

        var result = await CreateController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(new[] { "ORO-RECENT-NEWER", "ORO-RECENT-OLDER" }, model.RecentOrders.Select(order => order.Number));
        Assert.All(model.RecentOrders, order => Assert.Equal("Cliente Real", order.Customer?.TradeName));
    }

    private static DashboardController CreateController(
        Orofoods.Web.Data.ApplicationDbContext db,
        TimeProvider? timeProvider = null,
        WmcSyncCoordinator? wmcSyncCoordinator = null)
    {
        return new DashboardController(db, timeProvider ?? TimeProvider.System, wmcSyncCoordinator ?? new WmcSyncCoordinator());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
