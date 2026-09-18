using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Payments;

public sealed class MercadoPagoPaymentGateway(
    HttpClient httpClient,
    IOptions<MercadoPagoOptions> options) : IPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new MercadoPagoCreateOrderRequest(
            "online",
            "automatic",
            Money(request.Amount),
            request.ExternalReference,
            new MercadoPagoPayerRequest(request.PayerEmail),
            new MercadoPagoTransactionsRequest([
                new MercadoPagoPaymentRequest(
                    Money(request.Amount),
                    new MercadoPagoPaymentMethodRequest("pix", "bank_transfer"),
                    XmlConvert.ToString(request.Expiration))
            ]));
        return SendAsync(HttpMethod.Post, "v1/orders", payload, request.IdempotencyKey, cancellationToken);
    }

    public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new MercadoPagoCreateOrderRequest(
            "online",
            "automatic",
            Money(request.Amount),
            request.ExternalReference,
            new MercadoPagoPayerRequest(request.PayerEmail),
            new MercadoPagoTransactionsRequest([
                new MercadoPagoPaymentRequest(
                    Money(request.Amount),
                    new MercadoPagoPaymentMethodRequest(
                        request.PaymentMethodId,
                        "credit_card",
                        request.CardToken,
                        request.Installments))
            ]));
        return SendAsync(HttpMethod.Post, "v1/orders", payload, request.IdempotencyKey, cancellationToken);
    }

    public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"v1/orders/{Uri.EscapeDataString(gatewayOrderId)}", null, null, cancellationToken);

    public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"v1/orders/{Uri.EscapeDataString(request.GatewayOrderId)}/refund",
            new MercadoPagoRefundRequest([
                new MercadoPagoRefundTransactionRequest(request.GatewayPaymentId, Money(request.Amount))
            ]),
            request.IdempotencyKey,
            cancellationToken);

    private async Task<PaymentGatewayOrder> SendAsync(
        HttpMethod method,
        string path,
        object? payload,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        }
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException(response.StatusCode);
        }

        var order = await response.Content.ReadFromJsonAsync<MercadoPagoOrderResponse>(JsonOptions, cancellationToken)
            ?? throw new PaymentGatewayException(response.StatusCode);
        var payment = order.Transactions?.Payments.FirstOrDefault();
        var refund = order.Transactions?.Refunds.FirstOrDefault();
        var status = payment?.Status ?? order.Status;
        var statusDetail = payment?.StatusDetail ?? order.StatusDetail;
        var totalAmount = decimal.TryParse(order.TotalAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount)
            ? parsedAmount
            : 0m;

        return new PaymentGatewayOrder(
            order.Id,
            payment?.Id ?? refund?.TransactionId,
            order.ExternalReference,
            totalAmount,
            MercadoPagoStatusMapper.Map(status, statusDetail),
            payment?.PaymentMethod?.QrCode,
            payment?.PaymentMethod?.QrCodeBase64,
            payment?.DateOfExpiration?.UtcDateTime,
            payment?.PaymentMethod?.Type == "credit_card" ? payment.PaymentMethod.Id : null,
            payment?.PaymentMethod?.Installments,
            payment?.Card?.LastFourDigits,
            payment?.AuthorizationCode);
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
