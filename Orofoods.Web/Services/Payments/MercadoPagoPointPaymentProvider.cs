using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Payments;

/// <summary>Mercado Pago Point Orders API client. This provider is restricted to the Test host environment.</summary>
public sealed class MercadoPagoPointPaymentProvider(
    HttpClient httpClient,
    IOptions<MercadoPagoPointOptions> options,
    IHostEnvironment hostEnvironment) : IPointPaymentProvider
{
    private const string VirtualDeviceId = "SBX0000001";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PointPaymentResult> CreateTerminalPaymentAsync(PointPaymentRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTestConfiguration(request.DeviceId);
        if (request.OrderId <= 0 || request.PaymentId <= 0 || request.AssignmentId <= 0 || request.Amount <= 0m ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new InvalidOperationException("Os dados da cobrança Point são inválidos.");
        }

        var externalReference = request.ExternalReference ?? $"oro-order-{request.OrderId}-attempt-{request.PaymentId}";
        var payload = new MercadoPagoPointCreateOrderRequest(
            "point",
            externalReference,
            new MercadoPagoPointTransactionsRequest([
                new MercadoPagoPointPaymentRequest(request.Amount.ToString("0.00", CultureInfo.InvariantCulture))
            ]),
            new MercadoPagoPointConfigRequest(
                new MercadoPagoPointTerminalConfigRequest($"{options.Value.PoiType}__{VirtualDeviceId}")));

        var order = await SendAsync<MercadoPagoPointOrderResponse>(
            HttpMethod.Post, "v1/orders", payload, request.IdempotencyKey, cancellationToken);
        return Map(order);
    }

    public async Task<PointPaymentResult> GetPaymentStatusAsync(string externalPaymentId, CancellationToken cancellationToken = default)
    {
        EnsureTestConfiguration();
        if (string.IsNullOrWhiteSpace(externalPaymentId))
        {
            throw new InvalidOperationException("A referência da cobrança Point é inválida.");
        }

        var order = await SendAsync<MercadoPagoPointOrderResponse>(
            HttpMethod.Get, $"v1/orders/{Uri.EscapeDataString(externalPaymentId)}", null, null, cancellationToken);
        return Map(order);
    }

    public async Task<PointPaymentResult> CancelPendingPaymentAsync(string externalPaymentId, CancellationToken cancellationToken = default)
    {
        EnsureTestConfiguration();
        if (string.IsNullOrWhiteSpace(externalPaymentId))
        {
            throw new InvalidOperationException("A referência da cobrança Point é inválida.");
        }

        using var response = await SendRawAsync(
            HttpMethod.Post,
            $"v1/orders/{Uri.EscapeDataString(externalPaymentId)}/cancel",
            null,
            $"cancel-{externalPaymentId}",
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            // Acceptance of the cancel command is not proof the order is canceled; reconcile with GET.
            return new PointPaymentResult(
                PointPaymentState.Processing,
                "CANCEL_REQUESTED",
                "A solicitação de cancelamento foi enviada; o status ainda precisa ser confirmado.",
                PaymentStatus.Processing,
                GatewayOrderId: externalPaymentId);
        }

        var json = await response.Content.ReadFromJsonAsync<MercadoPagoPointOrderResponse>(JsonOptions, cancellationToken);
        return json is null ? throw new PaymentGatewayException(response.StatusCode) : Map(json);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? payload, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, path, payload, idempotencyKey, cancellationToken);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new PaymentGatewayException(response.StatusCode);
        }
        catch (JsonException)
        {
            throw new PaymentGatewayException(response.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(
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

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            response.Dispose();
            throw new PaymentGatewayException(statusCode);
        }
        return response;
    }

    private void EnsureTestConfiguration(string? deviceId = null)
    {
        if (!options.Value.Enabled)
        {
            throw new InvalidOperationException("A integração Mercado Pago Point está desabilitada.");
        }
        if (!hostEnvironment.IsEnvironment("Test") || !string.Equals(options.Value.Environment, "Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A integração Mercado Pago Point aceita chamadas somente em ambiente Test.");
        }
        if (string.IsNullOrWhiteSpace(options.Value.AccessToken))
        {
            throw new InvalidOperationException("Credenciais de teste Mercado Pago Point não configuradas.");
        }
        if (!string.IsNullOrWhiteSpace(deviceId) && !string.Equals(deviceId, VirtualDeviceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Esta fase aceita somente o dispositivo virtual de teste Mercado Pago Point.");
        }
        if (string.IsNullOrWhiteSpace(options.Value.PoiType) || options.Value.PoiType.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException("O tipo de terminal Mercado Pago Point está inválido.");
        }
    }

    private static PointPaymentResult Map(MercadoPagoPointOrderResponse order)
    {
        if (string.IsNullOrWhiteSpace(order.Id) || !string.Equals(order.Type, "point", StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentGatewayException(System.Net.HttpStatusCode.BadGateway);
        }

        var payments = order.Transactions?.Payments ?? [];
        // Point Orders are documented as having one payment transaction per order. Keep all
        // returned transactions so reconciliation can reject unexpected ambiguity instead of
        // silently approving whichever transaction happened to be first in the payload.
        var payment = payments.Count == 1 ? payments[0] : null;
        var amount = ParseAmount(order.TotalAmount);
        // The documented Point create response may omit aggregate total_amount while including
        // exactly one transaction payment amount. For a Point order, that sole amount is the
        // aggregate; approval still independently validates the transaction and paid amounts.
        if (!amount.Present && payment is not null)
        {
            amount = ParseAmount(payment.Amount);
        }
        var totalPaidAmount = ParseAmount(order.TotalPaidAmount);
        var transactionResults = payments.Select(MapTransaction).ToArray();
        var effectiveStatusDetail = payment?.StatusDetail ?? order.StatusDetail;
        var mappedStatus = MapStatus(order.Status, payment?.Status, effectiveStatusDetail);
        var state = mappedStatus switch
        {
            PaymentStatus.Pending => PointPaymentState.Created,
            PaymentStatus.Processing => PointPaymentState.Processing,
            PaymentStatus.ActionRequired => PointPaymentState.ActionRequired,
            PaymentStatus.Approved => PointPaymentState.Approved,
            PaymentStatus.Rejected => PointPaymentState.Rejected,
            PaymentStatus.Cancelled => PointPaymentState.Cancelled,
            PaymentStatus.Expired => PointPaymentState.Expired,
            PaymentStatus.Refunded => PointPaymentState.Refunded,
            _ => PointPaymentState.Unknown
        };

        return new PointPaymentResult(
            state,
            mappedStatus is null ? "UNKNOWN_POINT_STATUS" : "",
            mappedStatus is null ? "O status retornado pelo Mercado Pago Point não é reconhecido." : "Status Mercado Pago Point consultado.",
            mappedStatus,
            order.Id,
            payment?.Id,
            order.ExternalReference,
            amount.Value,
            order.Status,
            effectiveStatusDetail,
            new PointPaymentFinancials(
                totalPaidAmount.Value,
                totalPaidAmount.Present,
                totalPaidAmount.Invalid,
                transactionResults));
    }

    private static PointPaymentTransaction MapTransaction(MercadoPagoPointPaymentResponse payment)
    {
        var amount = ParseAmount(payment.Amount);
        var paidAmount = ParseAmount(payment.PaidAmount);
        return new PointPaymentTransaction(
            payment.Id,
            payment.Status,
            payment.StatusDetail,
            amount.Value,
            amount.Present,
            amount.Invalid,
            paidAmount.Value,
            paidAmount.Present,
            paidAmount.Invalid);
    }

    private static (decimal? Value, bool Present, bool Invalid) ParseAmount(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return (null, false, false);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return (null, true, true);
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
        return text is not null && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed, true, false)
            : (null, true, true);
    }

    private static PaymentStatus? MapStatus(string? orderStatus, string? paymentStatus, string? paymentStatusDetail) =>
        orderStatus?.ToLowerInvariant() switch
        {
            "created" or "pending" => PaymentStatus.Pending,
            "at_terminal" => PaymentStatus.Processing,
            "action_required" => PaymentStatus.ActionRequired,
            "processed" when string.Equals(paymentStatus, "processed", StringComparison.OrdinalIgnoreCase)
                              && (string.IsNullOrWhiteSpace(paymentStatusDetail)
                                  || string.Equals(paymentStatusDetail, "accredited", StringComparison.OrdinalIgnoreCase))
                => PaymentStatus.Approved,
            "failed" => PaymentStatus.Rejected,
            "canceled" => PaymentStatus.Cancelled,
            "expired" => PaymentStatus.Expired,
            "refunded" => PaymentStatus.Refunded,
            _ => null
        };

    private sealed record MercadoPagoPointCreateOrderRequest(
        string Type,
        string ExternalReference,
        MercadoPagoPointTransactionsRequest Transactions,
        MercadoPagoPointConfigRequest Config);

    private sealed record MercadoPagoPointTransactionsRequest(IReadOnlyList<MercadoPagoPointPaymentRequest> Payments);
    private sealed record MercadoPagoPointPaymentRequest(string Amount);
    private sealed record MercadoPagoPointConfigRequest(MercadoPagoPointTerminalConfigRequest Point);
    private sealed record MercadoPagoPointTerminalConfigRequest(string TerminalId);

    private sealed class MercadoPagoPointOrderResponse
    {
        public string Id { get; init; } = "";
        public string? Type { get; init; }
        public string? ExternalReference { get; init; }
        public JsonElement TotalAmount { get; init; }
        public string? Status { get; init; }
        public string? StatusDetail { get; init; }
        public JsonElement TotalPaidAmount { get; init; }
        public MercadoPagoPointTransactionsResponse? Transactions { get; init; }
    }

    private sealed class MercadoPagoPointTransactionsResponse
    {
        public List<MercadoPagoPointPaymentResponse> Payments { get; init; } = [];
    }

    private sealed class MercadoPagoPointPaymentResponse
    {
        public string? Id { get; init; }
        public JsonElement Amount { get; init; }
        public JsonElement PaidAmount { get; init; }
        public string? Status { get; init; }
        public string? StatusDetail { get; init; }
    }
}
