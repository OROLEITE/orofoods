using System.Globalization;
using System.Text;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcOrderFileGenerator
{
    private static readonly int[] SupportedPaymentDays = [0, 7, 14];

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
            BuildPaymentRecord021(order),
            BuildBlankRecord("030", 122)
        };

        foreach (var item in order.Items.Select((value, index) => (value, index)))
        {
            lines.Add(BuildItemRecord(item.value, item.index + 1));
        }

        lines.Add(BuildTrailer(customerCode));
        return new WmcOrderFileResult(true, string.Join(Environment.NewLine, lines), []);
    }

    public IReadOnlyList<string> Validate(Order order)
    {
        var errors = new List<string>();
        var paymentDays = GetWmcPaymentDays(order.PaymentTerm);
        if (paymentDays is null || !SupportedPaymentDays.Contains(paymentDays.Value))
        {
            errors.Add("WMC_PAYMENT_TERM_UNSUPPORTED");
        }

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

    internal static string BuildPaymentRecord021(Order order)
    {
        var paymentDays = GetWmcPaymentDays(order.PaymentTerm!)!.Value;
        var createdAt = order.CreatedAt == default ? DateTime.UtcNow : order.CreatedAt;
        var dueDate = createdAt.Date.AddDays(paymentDays);
        return "021  5  1  CD "
            + paymentDays.ToString("D3", CultureInfo.InvariantCulture)
            + dueDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            + FixedLeft(ToCents(order.Total), 15)
            + "10000";
    }

    private static int? GetWmcPaymentDays(PaymentTerm? paymentTerm) => paymentTerm?.Code switch
    {
        "PIX" or "CASH" when paymentTerm.DaysUntilDue == 0 => 0,
        "CREDIT_CARD" or "CARD_ON_DELIVERY" => 0,
        "BOLETO_7D" when paymentTerm.DaysUntilDue == 7 => 7,
        "BOLETO_14D" when paymentTerm.DaysUntilDue == 14 => 14,
        _ => null
    };

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
