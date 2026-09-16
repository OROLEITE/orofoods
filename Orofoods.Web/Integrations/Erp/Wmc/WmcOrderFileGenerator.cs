using System.Globalization;
using System.Text;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcOrderFileGenerator
{
    public WmcOrderFileResult Build(Order order)
    {
        var errors = Validate(order);
        if (errors.Count > 0)
        {
            return new WmcOrderFileResult(false, null, errors);
        }

        var customerCode = order.Customer!.WmcCode!.Trim();
        var lines = new List<string>
        {
            BuildHeader(order),
            BuildCustomerRecord(order, customerCode),
            BuildBlankRecord("030", 122)
        };

        foreach (var item in order.Items.Select((value, index) => (value, index)))
        {
            lines.Add(BuildItemRecord(item.value, item.index + 1));
        }

        lines.Add(BuildTrailer(customerCode));
        return new WmcOrderFileResult(true, string.Join(Environment.NewLine, lines), []);
    }

    private static List<string> Validate(Order order)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(order.Customer?.WmcCode))
        {
            errors.Add("O codigo WMC do cliente e obrigatorio para exportar o pedido.");
        }

        foreach (var item in order.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Product?.WmcCode))
            {
                errors.Add($"O codigo WMC do produto '{item.ProductNameSnapshot}' e obrigatorio para exportar o pedido.");
            }
        }

        return errors;
    }

    private static string BuildHeader(Order order)
    {
        var orderReference = DigitsOnly(order.Number);
        var createdAt = order.CreatedAt == default ? DateTime.UtcNow : order.CreatedAt;
        var payload = new StringBuilder("019");
        payload.Append("  001");
        payload.Append(FixedRight(orderReference, 15));
        payload.Append(FixedRight(orderReference, 20));
        payload.Append(createdAt.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
        payload.Append(createdAt.AddDays(7).ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
        payload.Append(createdAt.AddDays(7).ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
        payload.Append("CIF");
        return FixedRight(payload.ToString(), 315);
    }

    private static string BuildCustomerRecord(Order order, string customerCode)
    {
        var createdAt = order.CreatedAt == default ? DateTime.UtcNow : order.CreatedAt;
        var payload = new StringBuilder("021");
        payload.Append("  5  1  CD ");
        payload.Append(createdAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        payload.Append(FixedLeft(customerCode, 12));
        return FixedRight(payload.ToString(), 45);
    }

    private static string BuildItemRecord(OrderItem item, int sequence)
    {
        var product = item.Product!;
        var payload = new StringBuilder("040");
        payload.Append(sequence.ToString("000", CultureInfo.InvariantCulture));
        payload.Append("00000   EN ");
        payload.Append(FixedRight(product.WmcCode!.Trim(), 13));
        payload.Append(FixedRight(NormalizeDescription(item.ProductNameSnapshot), 60));
        payload.Append(FixedRight(product.Unit.ToUpperInvariant(), 3));
        payload.Append(FixedLeft(item.Quantity.ToString(CultureInfo.InvariantCulture), 6));
        payload.Append(FixedLeft(ToCents(item.UnitPrice), 15));
        payload.Append(FixedLeft(ToCents(item.Subtotal), 15));
        return FixedRight(payload.ToString(), 330);
    }

    private static string BuildTrailer(string customerCode)
    {
        var payload = new StringBuilder("090");
        payload.Append(FixedLeft(customerCode, 12));
        payload.Append(FixedLeft(customerCode, 12));
        return FixedRight(payload.ToString(), 122);
    }

    private static string BuildBlankRecord(string recordType, int length) => FixedRight(recordType, length, '0');

    private static string FixedRight(string value, int length, char padding = ' ') =>
        value.Length > length ? value[..length] : value.PadRight(length, padding);

    private static string FixedLeft(string value, int length) =>
        value.Length > length ? value[^length..] : value.PadLeft(length, '0');

    private static string DigitsOnly(string value) => new(value.Where(char.IsDigit).ToArray());

    private static string NormalizeDescription(string value) => new(value
        .ToUpperInvariant()
        .Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
        .ToArray());

    private static string ToCents(decimal value) => decimal.Truncate(value * 100m).ToString("0", CultureInfo.InvariantCulture);
}

public sealed record WmcOrderFileResult(bool Succeeded, string? Content, IReadOnlyList<string> Errors);
