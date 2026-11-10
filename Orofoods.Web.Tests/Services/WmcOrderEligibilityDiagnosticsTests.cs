using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcOrderEligibilityDiagnosticsTests
{
    [Fact]
    public async Task Order_17_above_marker_16_is_eligible_for_selection_and_reservation_without_tracking()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Orders.Add(CreateOrder(17, OrderStatus.Approved, IntegrationStatus.Pending));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var diagnostic = await ReadOrderDiagnosticAsync(CreateQuery(db), 17);

        Assert.True((bool)Get(diagnostic, "MatchesReservationPredicates"));
        Assert.True((bool)Get(diagnostic, "IncludedInNextBatch"));
        Assert.Empty((IEnumerable<string>)Get(diagnostic, "ExclusionReasons"));
        Assert.Empty(db.ChangeTracker.Entries<Order>());
    }

    [Fact]
    public async Task Manual_review_is_reported_as_an_exclusion_without_returning_private_error_text()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder(17, OrderStatus.Approved, IntegrationStatus.Pending);
        order.WmcOrderExportState = new WmcOrderExportState
        {
            Status = WmcOrderExportStatus.Pending,
            RequiresManualReview = true,
            LastError = "private customer detail"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var diagnostic = await ReadOrderDiagnosticAsync(CreateQuery(db), 17);
        var reasons = (IEnumerable<string>)Get(diagnostic, "ExclusionReasons");

        Assert.False((bool)Get(diagnostic, "MatchesReservationPredicates"));
        Assert.Contains("manual-review", reasons);
        Assert.DoesNotContain("private customer detail", string.Join(" ", reasons), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_at_marker_16_is_excluded_when_marker_is_16()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Orders.Add(CreateOrder(16, OrderStatus.Approved, IntegrationStatus.Pending));
        await db.SaveChangesAsync();

        var diagnostic = await ReadOrderDiagnosticAsync(CreateQuery(db, cutoff: 16), 16);
        var reasons = (IEnumerable<string>)Get(diagnostic, "ExclusionReasons");

        Assert.False((bool)Get(diagnostic, "AboveActivationCutoff"));
        Assert.False((bool)Get(diagnostic, "MatchesReservationPredicates"));
        Assert.Contains("at-or-below-cutoff", reasons);
    }

    [Fact]
    public async Task Blocking_audit_is_reported_by_a_safe_reason_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = CreateOrder(17, OrderStatus.Approved, IntegrationStatus.Pending);
        order.WmcExportAudits.Add(new WmcExportAudit
        {
            Source = WmcExportSource.Automatic,
            Outcome = WmcExportOutcome.Unknown
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var diagnostic = await ReadOrderDiagnosticAsync(CreateQuery(db), 17);
        var reasons = (IEnumerable<string>)Get(diagnostic, "ExclusionReasons");

        Assert.True((bool)Get(diagnostic, "HasBlockingAudit"));
        Assert.False((bool)Get(diagnostic, "MatchesReservationPredicates"));
        Assert.Contains("blocking-export-audit", reasons);
    }

    [Fact]
    public async Task Read_only_diagnostic_reports_an_eligible_order_outside_the_twenty_order_batch()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente de teste", TradeName = "Cliente", Cnpj = "03.932.292/0001-99" };
        var creator = TestDbContextFactory.CreateOrderCreator("wmc-diagnostics-batch-test");
        for (var id = 1; id <= 21; id++)
        {
            var order = CreateOrder(id, OrderStatus.Approved, IntegrationStatus.Pending, customer, creator);
            order.CreatedAt = DateTime.UtcNow.AddMinutes(id);
            db.Orders.Add(order);
        }
        await db.SaveChangesAsync();

        var diagnostic = await ReadOrderDiagnosticAsync(CreateQuery(db, cutoff: 0), 21);
        var reasons = (IEnumerable<string>)Get(diagnostic, "ExclusionReasons");

        Assert.True((bool)Get(diagnostic, "MatchesReservationPredicates"));
        Assert.False((bool)Get(diagnostic, "IncludedInNextBatch"));
        Assert.Contains("batch-limit-20", reasons);
    }

    private static async Task<object> ReadOrderDiagnosticAsync(WmcActivationDiagnosticsQuery query, int orderId)
    {
        var method = typeof(WmcActivationDiagnosticsQuery).GetMethod(
            "ReadAsync",
            [typeof(int?), typeof(CancellationToken)]);
        Assert.NotNull(method);
        var task = (Task)method.Invoke(query, [orderId, CancellationToken.None])!;
        await task;
        var response = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return Get(response, "OrderEligibility");
    }

    private static object Get(object instance, string property)
    {
        var target = instance.GetType().GetProperty(property);
        Assert.NotNull(target);
        return target.GetValue(instance)!;
    }

    private static WmcActivationDiagnosticsQuery CreateQuery(ApplicationDbContext db, int cutoff = 16) => new(
        db,
        Options.Create(new WmcJsonOrderOptions
        {
            Enabled = true,
            AutomaticWorkerEnabled = true,
            AutomaticWorkerStartAfterOrderId = cutoff
        }),
        Options.Create(new WmcFileDropOptions { Enabled = false }));

    private static Order CreateOrder(
        int id,
        OrderStatus status,
        IntegrationStatus integrationStatus,
        Customer? customer = null,
        Orofoods.Web.Models.Identity.ApplicationUser? creator = null) => new()
    {
        Id = id,
        Number = $"ORO-2026-{id:000000}",
        Status = status,
        IntegrationStatus = integrationStatus,
        Customer = customer ?? new Customer { LegalName = "Cliente de teste", TradeName = "Cliente", Cnpj = $"03.932.292/0000-{id:00}" },
        CreatedByUser = creator ?? TestDbContextFactory.CreateOrderCreator($"wmc-diagnostics-order-{id}")
    };
}
