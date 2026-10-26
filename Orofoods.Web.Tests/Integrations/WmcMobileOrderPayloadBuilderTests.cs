using System.Text.Json;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Tests.Integrations;

public class WmcMobileOrderPayloadBuilderTests
{
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
