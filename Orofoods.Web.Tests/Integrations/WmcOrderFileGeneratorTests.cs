using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;

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

        order.Total = 758.40m;
        order.PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 };
        var result = new WmcOrderFileGenerator().Build(order);
        var lines = result.Content!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.True(result.Succeeded);
        Assert.Equal([315, 45, 122, 330, 122], lines.Select(line => line.Length));
        Assert.StartsWith("019", lines[0]);
        Assert.Contains("00020260830", lines[1]);
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

    [Fact]
    public void Exports_card_on_delivery_as_due_today_without_changing_the_payment_term()
    {
        var order = new Order
        {
            Number = "ORO-2026-000778",
            CreatedAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { WmcCode = "107072" },
            PaymentTerm = new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", DaysUntilDue = 14 },
            Total = 63.20m,
            Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 63.20m, Subtotal = 63.20m }]
        };

        var result = new WmcOrderFileGenerator().Build(order);
        var paymentRecord = result.Content?.Split(Environment.NewLine)[1];

        Assert.True(result.Succeeded);
        Assert.Equal(45, paymentRecord?.Length);
        Assert.Contains("00020260830", paymentRecord);
        Assert.Equal(14, order.PaymentTerm.DaysUntilDue);
    }
}
