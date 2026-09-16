using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Tests.Integrations;

public class WmcOrderFileGeneratorTests
{
    [Fact]
    public void Builds_a_fixed_width_homologation_file_for_a_mapped_order()
    {
        var order = new Order
        {
            Number = "ORO-2026-000777",
            CreatedAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { WmcCode = "107072" },
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "610601552", Unit = "caixa" },
                    ProductNameSnapshot = "Pao brioche congelado",
                    Quantity = 12,
                    UnitPrice = 63.20m,
                    Subtotal = 758.40m
                }
            ]
        };

        var result = new WmcOrderFileGenerator().Build(order);
        var lines = result.Content!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.True(result.Succeeded);
        Assert.Equal([315, 45, 122, 330, 122], lines.Select(line => line.Length));
        Assert.StartsWith("019", lines[0]);
        Assert.Contains("107072", lines[1]);
        Assert.Contains("610601552", lines[3]);
        Assert.StartsWith("090", lines[4]);
    }

    [Fact]
    public void Rejects_order_when_a_wmc_mapping_is_missing()
    {
        var order = new Order
        {
            Customer = new Customer(),
            Items = [new OrderItem { Product = new Product(), ProductNameSnapshot = "Pao", Quantity = 1 }]
        };

        var result = new WmcOrderFileGenerator().Build(order);

        Assert.False(result.Succeeded);
        Assert.Null(result.Content);
        Assert.Contains(result.Errors, error => error.Contains("cliente", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("produto", StringComparison.OrdinalIgnoreCase));
    }
}
