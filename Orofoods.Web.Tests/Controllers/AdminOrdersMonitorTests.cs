using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;
using Orofoods.Web.Api;
using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminOrdersMonitorTests
{
    [Fact]
    public async Task Index_defaults_to_the_current_sao_paulo_month()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        var creator = TestDbContextFactory.CreateOrderCreator("month-user");
        db.Orders.AddRange(
            CreateOrder(customer, creator, "BEFORE", new DateTime(2026, 10, 1, 2, 59, 59, DateTimeKind.Utc)),
            CreateOrder(customer, creator, "START", new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc)),
            CreateOrder(customer, creator, "END", new DateTime(2026, 11, 1, 2, 59, 59, DateTimeKind.Utc)),
            CreateOrder(customer, creator, "AFTER", new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero)));

        var result = await controller.Index();

        var orders = Assert.IsAssignableFrom<IReadOnlyList<Order>>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { "END", "START" }, orders.Select(order => order.Number));
        Assert.Equal(new DateOnly(2026, 10, 1), (DateOnly?)controller.ViewData["FromDate"]);
        Assert.Equal(new DateOnly(2026, 10, 31), (DateOnly?)controller.ViewData["ToDate"]);
        Assert.Equal(2, (int)controller.ViewData["TotalOrders"]!);
    }

    [Fact]
    public async Task Awaiting_action_scope_matches_dashboard_count_without_a_default_period()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        var creator = TestDbContextFactory.CreateOrderCreator("pending-user");
        db.Orders.AddRange(
            CreateOrder(customer, creator, "OLD-RECEIVED", new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Received),
            CreateOrder(customer, creator, "OLD-REVIEW", new DateTime(2025, 8, 1, 12, 0, 0, DateTimeKind.Utc), OrderStatus.UnderReview),
            CreateOrder(customer, creator, "CURRENT-RECEIVED", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Received),
            CreateOrder(customer, creator, "CURRENT-REVIEW", new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc), OrderStatus.UnderReview),
            CreateOrder(customer, creator, "CURRENT-APPROVED", new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Approved));
        await db.SaveChangesAsync();

        var dashboardResult = await new DashboardController(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero)), new WmcSyncCoordinator()).Index();
        var dashboard = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(dashboardResult).Model);
        var controller = CreateController(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero)));

        var result = await controller.Index(scope: "awaiting-action");

        var paged = Assert.IsType<PagedResult<Order>>(controller.ViewData["PagedResult"]);
        var orders = Assert.IsAssignableFrom<IReadOnlyList<Order>>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(dashboard.PendingOrders, paged.TotalItems);
        Assert.Equal(new[] { "CURRENT-REVIEW", "CURRENT-RECEIVED", "OLD-REVIEW", "OLD-RECEIVED" }, orders.Select(order => order.Number));
        Assert.Null(controller.ViewData["FromDate"]);
        Assert.Null(controller.ViewData["ToDate"]);
        Assert.Equal(4, (int)controller.ViewData["AwaitingActionCount"]!);
    }

    [Fact]
    public async Task Index_classifies_wmc_states_and_highlights_delivered_orders_still_pending()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer();
        var creator = TestDbContextFactory.CreateOrderCreator("wmc-user");
        var created = CreateOrder(customer, creator, "NO-EXPORT", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        var exported = CreateOrder(customer, creator, "EXPORTED", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        exported.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported };
        var imported = CreateOrder(customer, creator, "IMPORTED", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        imported.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Imported };
        var rejected = CreateOrder(customer, creator, "REJECTED", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        rejected.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Rejected };
        var error = CreateOrder(customer, creator, "ERROR", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        error.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Error };
        var review = CreateOrder(customer, creator, "REVIEW", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        review.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported, RequiresManualReview = true };
        var legacyReview = CreateOrder(customer, creator, "LEGACY-REVIEW", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        legacyReview.WmcExportAudits.Add(new WmcExportAudit { ExportedAt = DateTime.UtcNow, Succeeded = false, Error = "Legacy audit" });
        var pendingState = CreateOrder(customer, creator, "PENDING-STATE", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        pendingState.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Pending };
        var processingState = CreateOrder(customer, creator, "PROCESSING-STATE", new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Delivered);
        processingState.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Processing };
        db.Orders.AddRange(created, exported, imported, rejected, error, review, legacyReview, pendingState, processingState);
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero)));

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        Assert.Equal(9, (int)controller.ViewData["TotalOrders"]!);
        Assert.Equal(1, (int)controller.ViewData["WmcAwaitingConfirmationCount"]!);
        Assert.Equal(4, (int)controller.ViewData["WmcAttentionCount"]!);
        var deliveredPendingIds = Assert.IsAssignableFrom<IReadOnlySet<int>>(controller.ViewData["DeliveredWmcPendingOrderIds"]);
        Assert.Equal(new[] { created.Id, exported.Id, rejected.Id, error.Id, review.Id, legacyReview.Id, pendingState.Id, processingState.Id },
            deliveredPendingIds.Order().ToArray());

        await controller.Index(wmcStatus: "pending");
        var pending = Assert.IsType<PagedResult<Order>>(controller.ViewData["PagedResult"]);
        Assert.Equal(new[] { "LEGACY-REVIEW", "NO-EXPORT", "PENDING-STATE", "PROCESSING-STATE", "REVIEW" }, pending.Items.Select(order => order.Number).Order());

        await controller.Index(wmcStatus: "exported");
        var awaitingConfirmation = Assert.IsType<PagedResult<Order>>(controller.ViewData["PagedResult"]);
        Assert.Equal(new[] { "EXPORTED" }, awaitingConfirmation.Items.Select(order => order.Number));

        foreach (var (filter, expectedNumber) in new[]
        {
            ("imported", "IMPORTED"),
            ("rejected", "REJECTED"),
            ("error", "ERROR")
        })
        {
            await controller.Index(wmcStatus: filter);
            var matching = Assert.IsType<PagedResult<Order>>(controller.ViewData["PagedResult"]);
            Assert.Equal(new[] { expectedNumber }, matching.Items.Select(order => order.Number));
        }
    }

    [Fact]
    public async Task Index_applies_filters_before_pagination_and_exposes_the_active_query_for_links()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente", TradeName = "PAGE customer", Cnpj = "12.345.678/0001-99" };
        var creator = TestDbContextFactory.CreateOrderCreator("page-user");
        var matchingOrders = Enumerable.Range(1, 27).Select(index =>
        {
            var order = CreateOrder(customer, creator, $"PAGE-{index:D2}", new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Approved);
            order.Total = index;
            order.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported };
            return order;
        }).ToArray();
        var outsideDate = CreateOrder(customer, creator, "PAGE-OUTSIDE-DATE", new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Approved);
        outsideDate.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported };
        var wrongStatus = CreateOrder(customer, creator, "PAGE-WRONG-STATUS", new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Received);
        wrongStatus.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported };
        var wrongWmc = CreateOrder(customer, creator, "PAGE-WRONG-WMC", new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Approved);
        wrongWmc.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Rejected };
        var otherCustomer = new Customer { LegalName = "Outro cliente", TradeName = "OTHER customer", Cnpj = "98.765.432/0001-10" };
        var wrongSearch = CreateOrder(otherCustomer, creator, "OTHER-SEARCH", new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), OrderStatus.Approved);
        wrongSearch.WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Exported };
        db.Orders.AddRange(matchingOrders.Append(outsideDate).Append(wrongStatus).Append(wrongWmc).Append(wrongSearch));
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero)));

        var result = await controller.Index(
            q: "PAGE",
            status: nameof(OrderStatus.Approved),
            sort: "number",
            direction: "asc",
            page: 2,
            fromDate: new DateOnly(2026, 10, 5),
            toDate: new DateOnly(2026, 10, 5),
            wmcStatus: "exported");

        var paged = Assert.IsType<PagedResult<Order>>(controller.ViewData["PagedResult"]);
        var orders = Assert.IsAssignableFrom<IReadOnlyList<Order>>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(27, paged.TotalItems);
        Assert.Equal(2, paged.TotalPages);
        Assert.Equal(2, orders.Count);
        Assert.Equal(new[] { "PAGE-26", "PAGE-27" }, orders.Select(order => order.Number));
        Assert.Equal("PAGE", controller.ViewData["Query"]);
        Assert.Equal(nameof(OrderStatus.Approved), controller.ViewData["Status"]);
        Assert.Equal("number", controller.ViewData["Sort"]);
        Assert.Equal("asc", controller.ViewData["Direction"]);
        Assert.Equal(new DateOnly(2026, 10, 5), (DateOnly?)controller.ViewData["FromDate"]);
        Assert.Equal(new DateOnly(2026, 10, 5), (DateOnly?)controller.ViewData["ToDate"]);
        Assert.Equal("exported", controller.ViewData["WmcStatus"]);
    }

    private static OrdersController CreateController(Orofoods.Web.Data.ApplicationDbContext db, TimeProvider timeProvider) => new(
        db,
        service: null!,
        wmcManualExportService: new WmcManualExportService(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db)),
        pointPaymentService: null!,
        Options.Create(new MercadoPagoPointOptions()),
        Options.Create(new PaymentEligibilityOptions()),
        hostEnvironment: null!,
        timeProvider: timeProvider);

    private static Customer CreateCustomer() => new()
    {
        LegalName = "Cliente Ltda",
        TradeName = "Cliente",
        Cnpj = "12.345.678/0001-99"
    };

    private static Order CreateOrder(Customer customer, ApplicationUser creator, string number, DateTime createdAt, OrderStatus status = OrderStatus.Received) => new()
    {
        Number = number,
        Customer = customer,
        CreatedByUser = creator,
        CreatedAt = createdAt,
        Status = status,
        Total = 10m
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
