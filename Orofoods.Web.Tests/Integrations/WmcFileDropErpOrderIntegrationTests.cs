using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Tests.Integrations;

public class WmcFileDropErpOrderIntegrationTests
{
    [Fact]
    public void Does_not_enable_automatic_retry_by_default()
    {
        var options = new WmcFileDropOptions();

        Assert.False(options.AutoRetryEnabled);
    }

    [Fact]
    public async Task Writes_a_mapped_order_to_the_configured_directory_when_enabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = directory });
        var order = new Order
        {
            Number = "ORO-2026-000777",
            Customer = new Customer { WmcCode = "107072" },
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "610601552", Unit = "caixa" },
                    ProductNameSnapshot = "Pao brioche",
                    Quantity = 2,
                    UnitPrice = 50m,
                    Subtotal = 100m
                }
            ]
        };

        var result = await new WmcFileDropErpOrderIntegration(options, new WmcOrderFileGenerator()).SendOrderAsync(order);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ExternalOrderId);
        Assert.True(File.Exists(Path.Combine(directory, result.ExternalOrderId!)));
    }

    [Fact]
    public async Task Does_not_write_files_when_the_adapter_is_disabled()
    {
        var options = Options.Create(new WmcFileDropOptions { Enabled = false });

        var result = await new WmcFileDropErpOrderIntegration(options, new WmcOrderFileGenerator())
            .SendOrderAsync(new Order());

        Assert.False(result.Succeeded);
        Assert.Contains("desativada", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reprocessing_the_same_order_preserves_the_existing_wmc_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = directory });
        var order = new Order
        {
            Number = "ORO-2026-000778",
            Customer = new Customer { WmcCode = "107072" },
            Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }]
        };
        var sut = new WmcFileDropErpOrderIntegration(options, new WmcOrderFileGenerator());

        var first = await sut.SendOrderAsync(order);
        var path = Path.Combine(directory, first.ExternalOrderId!);
        await File.WriteAllTextAsync(path, "arquivo-original");
        var second = await sut.SendOrderAsync(order);

        Assert.True(second.Succeeded);
        Assert.Equal(first.ExternalOrderId, second.ExternalOrderId);
        Assert.Equal("arquivo-original", await File.ReadAllTextAsync(path));
    }
}
