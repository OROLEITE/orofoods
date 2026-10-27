using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcMobileOrderPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Build(Order order, int wmcTransmissionId, IReadOnlyDictionary<int, int> salesRepresentativeCodes)
    {
        if (order.Id <= 0)
        {
            throw new WmcOrderMappingException("O pedido precisa estar persistido antes da transmissão WMC.");
        }

        if (wmcTransmissionId <= 0)
        {
            throw new WmcOrderMappingException("WMC transmission id must be positive.");
        }

        var customer = order.Customer;
        var customerCode = customer?.WmcCode;
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            throw new WmcOrderMappingException("O código WMC real do cliente não está configurado.");
        }

        if (customer?.SalesRepresentativeId is not int representativeId ||
            !salesRepresentativeCodes.TryGetValue(representativeId, out var representativeCode) ||
            representativeCode <= 0 || representativeCode > short.MaxValue)
        {
            throw new WmcOrderMappingException("O código WMC real do vendedor do pedido não está configurado.");
        }

        foreach (var item in order.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Product?.WmcCode))
            {
                throw new WmcOrderMappingException($"O código WMC real do produto '{item.ProductNameSnapshot}' não está configurado.");
            }
            _ = WmcOrderUnitResolver.Resolve(item);
        }

        var rows = new List<Dictionary<string, object>> { BuildHeader(order, wmcTransmissionId, customerCode.Trim(), representativeCode) };
        rows.AddRange(order.Items.Select((item, index) => BuildItem(item, index + 1, wmcTransmissionId, representativeCode)));
        return JsonSerializer.Serialize(rows, JsonOptions);
    }

    private static Dictionary<string, object> BuildHeader(Order order, int wmcTransmissionId, string customerCode, int representativeCode)
    {
        var header = new Dictionary<string, object>
        {
            ["tabela"] = "pedidos",
            ["id"] = wmcTransmissionId,
            ["nropedido"] = 0,
            ["idcliente"] = order.CustomerId,
            ["codcliente"] = customerCode,
            ["totqtde"] = order.Items.Sum(item => item.Quantity),
            ["valprods"] = order.Subtotal,
            ["valtotal"] = order.Total,
            ["obs"] = order.Notes,
            ["codvendedor"] = representativeCode,
            // These two zero defaults are emitted by the APK when the order has no document series.
            ["nrodocumento"] = 0,
            ["nroserie"] = 0,
            ["valorfrete"] = order.Freight
        };

        if (order.CreatedAt != default)
        {
            var createdLocal = order.CreatedAt.ToLocalTime();
            header["data"] = createdLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            header["datahora"] = createdLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            header["horaped"] = createdLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        if (order.RequestedDeliveryDate is DateTime deliveryDate)
        {
            header["dataentrega"] = deliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return header;
    }

    private static Dictionary<string, object> BuildItem(OrderItem item, int sequence, int wmcTransmissionId, int representativeCode)
    {
        return new Dictionary<string, object>
        {
            ["tabela"] = "ped_prods",
            ["codvendedor"] = representativeCode,
            ["id"] = wmcTransmissionId,
            ["nropedido"] = 0,
            ["nroitem"] = sequence,
            ["codproduto"] = item.Product!.WmcCode!.Trim(),
            ["descricao"] = item.ProductNameSnapshot,
            ["qtde"] = item.Quantity,
            ["preco"] = item.UnitPrice,
            ["valtotal"] = item.Subtotal,
            ["preco_s_desc"] = item.UnitPrice,
            ["un"] = WmcOrderUnitResolver.Resolve(item),
            ["undig"] = WmcOrderUnitResolver.Resolve(item)
        };
    }

}

public sealed class WmcOrderMappingException(string message) : Exception(message);
