using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcOrderImportPolicyTests
{
    [Fact]
    public void A_confirmed_selected_approved_pending_order_is_importable()
    {
        var order = CreateOrder();

        Assert.True(WmcOrderImportPolicy.CanImport(order, CreateOptions()));
    }

    [Fact]
    public void Manual_import_is_disabled_by_default()
    {
        var order = CreateOrder();
        var options = CreateOptions();
        options.ManualImportEnabled = false;

        Assert.False(WmcOrderImportPolicy.CanImport(order, options));
    }

    [Theory]
    [InlineData(OrderStatus.Received, IntegrationStatus.Pending, WmcOrderExportStatus.Pending, false)]
    [InlineData(OrderStatus.Approved, IntegrationStatus.Processing, WmcOrderExportStatus.Pending, false)]
    [InlineData(OrderStatus.Approved, IntegrationStatus.Pending, WmcOrderExportStatus.Exported, false)]
    [InlineData(OrderStatus.Approved, IntegrationStatus.Pending, WmcOrderExportStatus.Imported, false)]
    public void Ineligible_order_states_do_not_allow_an_import(
        OrderStatus orderStatus,
        IntegrationStatus integrationStatus,
        WmcOrderExportStatus wmcStatus,
        bool expected)
    {
        var order = CreateOrder();
        order.Status = orderStatus;
        order.IntegrationStatus = integrationStatus;
        order.WmcOrderExportState!.Status = wmcStatus;

        Assert.Equal(expected, WmcOrderImportPolicy.CanImport(order, CreateOptions()));
    }

    [Theory]
    [InlineData(WmcExportOutcome.Started)]
    [InlineData(WmcExportOutcome.Unknown)]
    [InlineData(WmcExportOutcome.Succeeded)]
    [InlineData(WmcExportOutcome.Imported)]
    [InlineData(WmcExportOutcome.Generated)]
    [InlineData(WmcExportOutcome.Regenerated)]
    public void Existing_uncertain_successful_or_txt_audit_blocks_a_second_import(WmcExportOutcome outcome)
    {
        var order = CreateOrder();
        order.WmcExportAudits.Add(new WmcExportAudit { Outcome = outcome });

        Assert.False(WmcOrderImportPolicy.CanImport(order, CreateOptions()));
    }

    private static Order CreateOrder() => new()
    {
        Id = 782,
        Status = OrderStatus.Approved,
        IntegrationStatus = IntegrationStatus.Pending,
        WmcOrderExportState = new WmcOrderExportState { Status = WmcOrderExportStatus.Pending }
    };

    private static WmcJsonOrderOptions CreateOptions() => new()
    {
        Enabled = true,
        ManualImportEnabled = true
    };
}
