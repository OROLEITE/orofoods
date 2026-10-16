using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcOrderFileGenerator
{
    private static readonly int[] SupportedPaymentDays = [0, 7, 14];
    private readonly string supplierCnpj;

    public WmcOrderFileGenerator(IOptions<WmcOrderFileOptions> options)
    {
        if (!WmcOrderFileOptions.HasValidSupplierCnpj(options.Value))
        {
            throw new OptionsValidationException(
                WmcOrderFileOptions.SectionName,
                typeof(WmcOrderFileOptions),
                ["WmcOrderFile:SupplierCnpj deve conter exatamente 14 digitos."]);
        }

        supplierCnpj = options.Value.SupplierCnpj;
    }

    public WmcOrderFileResult Build(Order order)
    {
        var errors = Validate(order);
        if (errors.Count > 0)
        {
            return new WmcOrderFileResult(false, null, errors);
        }

        _ = TryGetExportableCustomerCnpj(order, out var customerCnpj, out _);

        var lines = new List<string>
        {
            BuildHeader(order, supplierCnpj, customerCnpj!),
            BuildPaymentRecord021(order),
            BuildDiscountChargesRecord(order)
        };

        foreach (var item in order.Items.Select((value, index) => (value, index)))
        {
            lines.Add(BuildItemRecord(item.value, item.index + 1));
        }

        lines.Add(BuildTrailer(order));
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

        if (!TryGetExportableCustomerCnpj(order, out _, out var customerCnpjError))
        {
            errors.Add(customerCnpjError!);
        }

        foreach (var item in order.Items)
        {
            if (string.Equals(item.SalesUnitCodeSnapshot, "PCT", StringComparison.OrdinalIgnoreCase)
                || (item.SalesUnitCodeSnapshot is null
                    && string.Equals(item.Product?.CommercialUnitCode, "PCT", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("WMC_PACKAGE_UNIT_CODE_UNCONFIRMED");
            }

            if (string.IsNullOrWhiteSpace(item.Product?.WmcCode))
            {
                errors.Add($"O codigo WMC do produto '{item.ProductNameSnapshot}' e obrigatorio para exportar o pedido.");
            }
        }

        return errors;
    }

    private static bool TryGetExportableCustomerCnpj(Order order, out string? normalizedCnpj, out string? error)
    {
        normalizedCnpj = null;
        if (string.IsNullOrWhiteSpace(order.CustomerCnpjSnapshot))
        {
            error = "WMC_CUSTOMER_CNPJ_SNAPSHOT_MISSING";
            return false;
        }

        if (!CnpjValidator.TryNormalize(order.CustomerCnpjSnapshot, out normalizedCnpj))
        {
            error = "WMC_CUSTOMER_CNPJ_INVALID";
            return false;
        }

        if (normalizedCnpj!.Take(12).Any(char.IsAsciiLetter))
        {
            error = "WMC_CUSTOMER_CNPJ_ALPHANUMERIC_LAYOUT_UNCONFIRMED";
            return false;
        }

        error = null;
        return true;
    }

    private static string BuildHeader(Order order, string supplierCnpj, string normalizedCustomerCnpj)
    {
        var orderReference = DigitsOnly(order.Number);
        var createdAt = order.CreatedAt == default ? DateTime.UtcNow : order.CreatedAt;
        var deliveryDate = order.RequestedDeliveryDate?.Date ?? createdAt.Date.AddDays(7);
        var payload = new StringBuilder("019");
        payload.Append("  001");
        payload.Append(FixedRight(orderReference, 20));
        payload.Append(FixedRight(orderReference, 20));
        payload.Append(createdAt.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
        payload.Append(deliveryDate.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
        payload.Append(deliveryDate.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
        payload.Append(Spaces(82));
        payload.Append(supplierCnpj);
        var customerCnpj = FixedLeft(normalizedCustomerCnpj, 14);
        payload.Append(customerCnpj);
        payload.Append(customerCnpj);
        payload.Append(customerCnpj);
        payload.Append(Spaces(3));
        payload.Append(Zeros(14));
        payload.Append(Spaces(30));
        payload.Append("CIF");
        payload.Append("000");
        payload.Append(Spaces(40));
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
        var unit = FixedRight(product.Unit.Trim().ToUpperInvariant(), 3);
        var payload = new StringBuilder("04");
        payload.Append(sequence.ToString("D4", CultureInfo.InvariantCulture));
        payload.Append("00000");
        payload.Append(Spaces(3));
        payload.Append(Spaces(3));
        payload.Append(FixedRight(product.WmcCode!.Trim(), 14));
        payload.Append(FixedRight(NormalizeDescription(item.ProductNameSnapshot), 40));
        payload.Append(Spaces(20));
        payload.Append(unit);
        payload.Append(FixedLeft(ToWholeNumber(product.WmcPackageQuantity ?? 0m), 5));
        payload.Append(FixedLeft(ToCents(item.Quantity), 15));
        payload.Append(Zeros(15));
        payload.Append(Zeros(15));
        payload.Append(Spaces(3));
        payload.Append(Zeros(5));
        payload.Append(FixedLeft(ToCents(item.UnitPrice * item.Quantity), 15));
        payload.Append(FixedLeft(ToCents(item.Subtotal), 15));
        payload.Append(FixedLeft(ToCents(item.UnitPrice), 15));
        payload.Append(FixedLeft(ToCents(item.UnitPrice), 15));
        payload.Append("00001");
        payload.Append(unit);
        payload.Append(Zeros(15));
        payload.Append(Zeros(5));
        payload.Append(Zeros(15));
        payload.Append(Zeros(5));
        payload.Append(Zeros(15));
        payload.Append(Zeros(15));
        payload.Append(Zeros(15));
        payload.Append(Zeros(7));
        payload.Append(Zeros(8));
        payload.Append(Spaces(10));
        return FixedRight(payload.ToString(), 330);
    }

    private static string BuildDiscountChargesRecord(Order order)
    {
        var payload = new StringBuilder("03");
        payload.Append(Zeros(5 + 15));
        payload.Append(Zeros(5 + 15));
        payload.Append(Zeros(5 + 15));
        payload.Append(Zeros(5 + 15));
        payload.Append(Zeros(5));
        payload.Append(FixedLeft(ToCents(order.Freight), 15));
        payload.Append(Zeros(5 + 15));
        return payload.ToString();
    }

    private static string BuildTrailer(Order order)
    {
        var payload = new StringBuilder("09");
        payload.Append(FixedLeft(ToCents(order.Subtotal), 15));
        payload.Append(Zeros(15));
        payload.Append(Zeros(15));
        payload.Append(FixedLeft(ToCents(order.Freight), 15));
        payload.Append(Zeros(15));
        payload.Append(Zeros(15));
        payload.Append(Zeros(15));
        payload.Append(FixedLeft(ToCents(order.Total), 15));
        return payload.ToString();
    }

    private static string Spaces(int length) => new(' ', length);

    private static string Zeros(int length) => new('0', length);

    private static string FixedRight(string value, int length, char padding = ' ') =>
        value.Length > length ? value[..length] : value.PadRight(length, padding);

    private static string FixedLeft(string value, int length) =>
        value.Length > length ? value[^length..] : value.PadLeft(length, '0');

    private static string DigitsOnly(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private static string NormalizeDescription(string value) => new(value
        .ToUpperInvariant()
        .Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
        .ToArray());

    private static string ToCents(decimal value) => decimal.Truncate(value * 100m).ToString("0", CultureInfo.InvariantCulture);

    private static string ToWholeNumber(decimal value) => decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture);
}

public sealed record WmcOrderFileResult(bool Succeeded, string? Content, IReadOnlyList<string> Errors);
