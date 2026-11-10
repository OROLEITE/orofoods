using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcActivationDiagnosticsQueryTests
{
    [Fact]
    public async Task Empty_orders_return_zero_aggregates_and_current_settings()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var query = CreateQuery(db, enabled: true, workerEnabled: false, cutoff: 42, fileDropEnabled: false);

        var result = await query.ReadAsync();

        Assert.Equal(0, result.MaxOrderId);
        Assert.Equal(0, result.TotalOrderCount);
        Assert.Equal(42, result.AutomaticWorkerStartAfterOrderId);
        Assert.True(result.WmcJsonOrderEnabled);
        Assert.False(result.AutomaticWorkerEnabled);
        Assert.False(result.WmcFileDropEnabled);
        Assert.Equal("Desativado pela configuração", result.AutomaticWorkerState);
    }

    [Fact]
    public async Task Existing_orders_return_max_id_count_and_effective_worker_state_without_tracking_orders()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente de teste", TradeName = "Cliente", Cnpj = "03.932.292/0001-99" };
        var creator = TestDbContextFactory.CreateOrderCreator("wmc-diagnostics-test");
        db.Orders.AddRange(
            new Order { Number = "WMC-DIAG-1", Customer = customer, CreatedByUser = creator, Status = OrderStatus.Approved },
            new Order { Number = "WMC-DIAG-2", Customer = customer, CreatedByUser = creator, Status = OrderStatus.Approved });
        await db.SaveChangesAsync();
        var expectedMaxId = await db.Orders.MaxAsync(order => order.Id);
        db.ChangeTracker.Clear();

        var result = await CreateQuery(db, enabled: true, workerEnabled: true, cutoff: 17, fileDropEnabled: false).ReadAsync();

        Assert.Equal(expectedMaxId, result.MaxOrderId);
        Assert.Equal(2, result.TotalOrderCount);
        Assert.Equal(17, result.AutomaticWorkerStartAfterOrderId);
        Assert.True(result.WmcJsonOrderEnabled);
        Assert.True(result.AutomaticWorkerEnabled);
        Assert.False(result.WmcFileDropEnabled);
        Assert.Equal("Habilitado pela configuração", result.AutomaticWorkerState);
        Assert.Empty(db.ChangeTracker.Entries<Order>());
    }

    [Theory]
    [InlineData(true, true, null, "Bloqueado: marco ausente ou inválido")]
    [InlineData(true, true, -1, "Bloqueado: marco ausente ou inválido")]
    [InlineData(false, true, 0, "Desativado pela configuração")]
    [InlineData(true, false, 0, "Desativado pela configuração")]
    public async Task Worker_state_reflects_gates_and_invalid_cutoff(
        bool enabled,
        bool workerEnabled,
        int? cutoff,
        string expectedState)
    {
        await using var db = await TestDbContextFactory.CreateAsync();

        var result = await CreateQuery(db, enabled, workerEnabled, cutoff, fileDropEnabled: false).ReadAsync();

        Assert.Equal(expectedState, result.AutomaticWorkerState);
    }

    private static WmcActivationDiagnosticsQuery CreateQuery(
        Orofoods.Web.Data.ApplicationDbContext db,
        bool enabled,
        bool workerEnabled,
        int? cutoff,
        bool fileDropEnabled) => new(
            db,
            Options.Create(new WmcJsonOrderOptions
            {
                Enabled = enabled,
                AutomaticWorkerEnabled = workerEnabled,
                AutomaticWorkerStartAfterOrderId = cutoff
            }),
            Options.Create(new WmcFileDropOptions { Enabled = fileDropEnabled }));
}
