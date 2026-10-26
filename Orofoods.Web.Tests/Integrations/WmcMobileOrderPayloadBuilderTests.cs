using System.Text.Json;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Tests.Integrations;

public class WmcMobileOrderPayloadBuilderTests
{
    [Theory]
    [InlineData("PC", "UN")]
    [InlineData("PCT", "UN")]
    [InlineData("CX", "CX")]
    [InlineData("KG", "KG")]
    public void Maps_only_pc_and_pct_to_un_without_changing_other_units(string productUnit, string expectedJsonUnit)
    {
        var order = new Order
        {
            Id = 4321,
            CustomerId = 17,
            Customer = new Customer { Id = 17, WmcCode = "WMC-CUST-17", SalesRepresentativeId = 27 },
            Items =
            [
                new OrderItem
                {
                    Product = new Orofoods.Web.Models.Catalog.Product
                    {
                        WmcCode = "504342",
                        Unit = productUnit,
                        CommercialUnitCode = "PCT"
                    },
                    ProductNameSnapshot = "Produto de teste",
                    Quantity = 3,
                    UnitPrice = 33.13m,
                    Subtotal = 99.39m
                }
            ]
        };

        var json = new WmcMobileOrderPayloadBuilder().Build(order, 9001, new Dictionary<int, int> { [27] = 804 });

        using var payload = JsonDocument.Parse(json);
        var item = payload.RootElement.EnumerateArray().Skip(1).Single();
        Assert.Equal(expectedJsonUnit, item.GetProperty("un").GetString());
        Assert.Equal(expectedJsonUnit, item.GetProperty("undig").GetString());
        Assert.Equal(3, item.GetProperty("qtde").GetInt32());
        Assert.Equal(33.13m, item.GetProperty("preco").GetDecimal());
        Assert.Equal(99.39m, item.GetProperty("valtotal").GetDecimal());
        Assert.Equal(productUnit, order.Items[0].Product!.Unit);
        Assert.Equal("PCT", order.Items[0].Product!.CommercialUnitCode);
    }

    [Fact]
    public void Omits_optional_values_without_source_data_and_never_serializes_json_null()
    {
        var order = new Order
        {
            Id = 4321,
            CustomerId = 17,
            Customer = new Customer { Id = 17, WmcCode = "WMC-CUST-17", SalesRepresentativeId = 27 },
            CreatedAt = default,
            Items = []
        };

        var json = new WmcMobileOrderPayloadBuilder().Build(order, 1, new Dictionary<int, int> { [27] = 804 });

        using var payload = JsonDocument.Parse(json);
        var header = Assert.Single(payload.RootElement.EnumerateArray());
        Assert.Equal("pedidos", header.GetProperty("tabela").GetString());
        Assert.Equal(1, header.GetProperty("id").GetInt32());
        Assert.Equal(0, header.GetProperty("nropedido").GetInt32());
        Assert.Equal("WMC-CUST-17", header.GetProperty("codcliente").GetString());
        Assert.Equal(0, header.GetProperty("nrodocumento").GetInt32());
        Assert.Equal(0, header.GetProperty("nroserie").GetInt32());
        Assert.False(header.TryGetProperty("data", out _));
        Assert.False(header.TryGetProperty("datahora", out _));
        Assert.False(header.TryGetProperty("horaped", out _));
        Assert.False(header.TryGetProperty("dataentrega", out _));
        Assert.False(header.TryGetProperty("percdesconto", out _));
        Assert.False(header.TryGetProperty("valdesconto", out _));
        Assert.All(header.EnumerateObject(), property => Assert.NotEqual(JsonValueKind.Null, property.Value.ValueKind));
    }
}
