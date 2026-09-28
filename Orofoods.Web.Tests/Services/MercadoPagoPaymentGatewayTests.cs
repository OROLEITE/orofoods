using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public class MercadoPagoPaymentGatewayTests
{
    [Fact]
    public async Task Pix_uses_orders_api_idempotency_and_does_not_send_a_pix_key()
    {
        var handler = new RecordingHandler(PixResponse);
        var gateway = CreateGateway(handler);

        var result = await gateway.CreatePixAsync(new CreatePixPaymentRequest(
            50m, "oro-order-42", "buyer@testuser.com", TimeSpan.FromMinutes(30), "attempt-key-1"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.mercadopago.com/v1/orders", request.Uri);
        Assert.Equal("Bearer not-a-real-credential", request.Authorization);
        Assert.Equal("attempt-key-1", request.IdempotencyKey);

        using var json = JsonDocument.Parse(request.Body!);
        var root = json.RootElement;
        Assert.Equal("online", root.GetProperty("type").GetString());
        Assert.Equal("automatic", root.GetProperty("processing_mode").GetString());
        Assert.Equal("50.00", root.GetProperty("total_amount").GetString());
        Assert.Equal("oro-order-42", root.GetProperty("external_reference").GetString());
        Assert.Equal("buyer@testuser.com", root.GetProperty("payer").GetProperty("email").GetString());
        var payment = root.GetProperty("transactions").GetProperty("payments")[0];
        Assert.Equal("50.00", payment.GetProperty("amount").GetString());
        Assert.Equal("PT30M", payment.GetProperty("expiration_time").GetString());
        Assert.Equal("pix", payment.GetProperty("payment_method").GetProperty("id").GetString());
        Assert.Equal("bank_transfer", payment.GetProperty("payment_method").GetProperty("type").GetString());
        Assert.DoesNotContain("pix_key", request.Body!, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("ORD-PIX-1", result.GatewayOrderId);
        Assert.Equal("PAY-PIX-1", result.GatewayPaymentId);
        Assert.Equal(PaymentStatus.Pending, result.Status);
        Assert.Equal("000201-copy-paste", result.PixCopyPaste);
        Assert.Equal("base64-image", result.PixQrCodeBase64);
        Assert.Equal(DateTimeOffset.Parse("2026-09-17T15:30:00Z").UtcDateTime, result.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt!.Value.Kind);
    }

    [Fact]
    public async Task Expiration_with_negative_offset_is_converted_to_the_correct_utc_instant()
    {
        var handler = new RecordingHandler(PixResponseWithExpiration("2026-09-17T18:00:00-03:00"));
        var gateway = CreateGateway(handler);

        var result = await gateway.CreatePixAsync(new CreatePixPaymentRequest(
            50m, "oro-order-46", "buyer@testuser.com", TimeSpan.FromMinutes(30), "attempt-key-5"));

        Assert.Equal(new DateTime(2026, 9, 17, 21, 0, 0, DateTimeKind.Utc), result.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt!.Value.Kind);
    }

    [Fact]
    public async Task Expiration_already_in_utc_keeps_the_same_instant()
    {
        var handler = new RecordingHandler(PixResponseWithExpiration("2026-09-17T21:00:00Z"));
        var gateway = CreateGateway(handler);

        var result = await gateway.CreatePixAsync(new CreatePixPaymentRequest(
            50m, "oro-order-47", "buyer@testuser.com", TimeSpan.FromMinutes(30), "attempt-key-6"));

        Assert.Equal(new DateTime(2026, 9, 17, 21, 0, 0, DateTimeKind.Utc), result.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt!.Value.Kind);
    }

    [Fact]
    public async Task Expiration_with_a_different_positive_offset_is_converted_to_the_correct_utc_instant()
    {
        var handler = new RecordingHandler(PixResponseWithExpiration("2026-09-17T23:30:00+02:30"));
        var gateway = CreateGateway(handler);

        var result = await gateway.CreatePixAsync(new CreatePixPaymentRequest(
            50m, "oro-order-48", "buyer@testuser.com", TimeSpan.FromMinutes(30), "attempt-key-7"));

        Assert.Equal(new DateTime(2026, 9, 17, 21, 0, 0, DateTimeKind.Utc), result.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt!.Value.Kind);
    }

    [Fact]
    public async Task Missing_expiration_stays_null()
    {
        var handler = new RecordingHandler(PixResponseWithoutExpiration);
        var gateway = CreateGateway(handler);

        var result = await gateway.CreatePixAsync(new CreatePixPaymentRequest(
            50m, "oro-order-49", "buyer@testuser.com", TimeSpan.FromMinutes(30), "attempt-key-8"));

        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public async Task Card_sends_only_token_allowed_metadata_and_authoritative_amount()
    {
        var handler = new RecordingHandler(CardResponse);
        var gateway = CreateGateway(handler);

        var result = await gateway.CreateCreditCardPaymentAsync(new CreateCreditCardPaymentRequest(
            125.40m, "oro-order-43", "buyer@testuser.com", "browser-token", "master", 3, "attempt-key-2"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("attempt-key-2", request.IdempotencyKey);
        using var json = JsonDocument.Parse(request.Body!);
        var method = json.RootElement.GetProperty("transactions").GetProperty("payments")[0].GetProperty("payment_method");
        Assert.Equal("browser-token", method.GetProperty("token").GetString());
        Assert.Equal("master", method.GetProperty("id").GetString());
        Assert.Equal("credit_card", method.GetProperty("type").GetString());
        Assert.Equal(3, method.GetProperty("installments").GetInt32());
        Assert.DoesNotContain("card_number", request.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("security_code", request.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvv", request.Body!, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal("master", result.CardBrand);
        Assert.Equal(3, result.Installments);
    }

    [Fact]
    public async Task Get_order_uses_the_documented_orders_resource()
    {
        var handler = new RecordingHandler(CardResponse);
        var gateway = CreateGateway(handler);

        await gateway.GetOrderAsync("ORD-CARD-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.mercadopago.com/v1/orders/ORD-CARD-1", request.Uri);
        Assert.Null(request.IdempotencyKey);
    }

    [Fact]
    public async Task Real_orders_api_response_maps_brand_installments_and_gateway_ids_but_has_no_last_four_or_authorization_code()
    {
        // Fixture mirrors the exact shape returned by a real GET /v1/orders/{id} call for an approved
        // card payment (values sanitized). The Orders API does not include a card object or
        // authorization_code at all, unlike the classic Payments API.
        var handler = new RecordingHandler(RealApprovedCardOrderResponse);
        var gateway = CreateGateway(handler);

        var result = await gateway.GetOrderAsync("ORD-CARD-REAL-1");

        Assert.Equal(PaymentStatus.Approved, result.Status);
        Assert.Equal("visa", result.CardBrand);
        Assert.Equal(1, result.Installments);
        Assert.Equal("PAY-CARD-REAL-1", result.GatewayPaymentId);
        Assert.Null(result.LastFourDigits);
        Assert.Null(result.AuthorizationCode);
    }

    [Fact]
    public async Task Refund_uses_order_and_transaction_identifiers_with_its_own_idempotency_key()
    {
        var handler = new RecordingHandler(RefundResponse, HttpStatusCode.Created);
        var gateway = CreateGateway(handler);

        var result = await gateway.RefundAsync(new RefundPaymentRequest(
            "ORD-CARD-1", "PAY-CARD-1", 125.40m, "refund-key-1"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.mercadopago.com/v1/orders/ORD-CARD-1/refund", request.Uri);
        Assert.Equal("refund-key-1", request.IdempotencyKey);
        using var json = JsonDocument.Parse(request.Body!);
        var transaction = json.RootElement.GetProperty("transactions")[0];
        Assert.Equal("PAY-CARD-1", transaction.GetProperty("id").GetString());
        Assert.Equal("125.40", transaction.GetProperty("amount").GetString());
        Assert.Equal(PaymentStatus.Refunded, result.Status);
    }

    [Fact]
    public async Task Gateway_error_does_not_expose_credentials_or_response_body()
    {
        var handler = new RecordingHandler("{\"message\":\"browser-token not-a-real-credential\"}", HttpStatusCode.BadRequest);
        var gateway = CreateGateway(handler);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.CreateCreditCardPaymentAsync(
            new CreateCreditCardPaymentRequest(1m, "order", "buyer@testuser.com", "browser-token", "visa", 1, "key")));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.DoesNotContain("browser-token", exception.Message);
        Assert.DoesNotContain("not-a-real-credential", exception.Message);
    }

    [Fact]
    public async Task Real_shape_rejected_card_response_maps_all_available_fields_and_keeps_optional_fields_null()
    {
        var handler = new RecordingHandler(CardRejectedResponse);
        var gateway = CreateGateway(handler);

        var result = await gateway.CreateCreditCardPaymentAsync(new CreateCreditCardPaymentRequest(
            60m, "oro-order-44", "buyer@testuser.com", "browser-token", "visa", 1, "attempt-key-3"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.mercadopago.com/v1/orders", request.Uri);
        Assert.Equal("attempt-key-3", request.IdempotencyKey);
        Assert.Equal("ORD-CARD-2", result.GatewayOrderId);
        Assert.Equal("PAY-CARD-2", result.GatewayPaymentId);
        Assert.Equal("oro-order-44", result.ExternalReference);
        Assert.Equal(60m, result.TotalAmount);
        Assert.Equal(PaymentStatus.Rejected, result.Status);
        Assert.Equal("visa", result.CardBrand);
        Assert.Equal(1, result.Installments);
        Assert.Null(result.LastFourDigits);
        Assert.Null(result.AuthorizationCode);
    }

    [Fact]
    public async Task Network_timeout_propagates_as_a_retryable_exception()
    {
        var handler = new TimeoutHandler();
        var gateway = CreateGateway(handler);

        await Assert.ThrowsAsync<TaskCanceledException>(() => gateway.CreatePixAsync(
            new CreatePixPaymentRequest(50m, "oro-order-45", "buyer@testuser.com", TimeSpan.FromMinutes(30), "attempt-key-4")));
    }

    private static MercadoPagoPaymentGateway CreateGateway(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") };
        return new MercadoPagoPaymentGateway(client, Options.Create(new MercadoPagoOptions
        {
            AccessToken = "not-a-real-credential"
        }));
    }

    private const string CardRejectedResponse = """
        {
          "id":"ORD-CARD-2","external_reference":"oro-order-44","total_amount":"60.00",
          "status":"processed","status_detail":"cc_rejected_insufficient_amount",
          "transactions":{"payments":[{"id":"PAY-CARD-2","amount":"60.00","status":"failed","status_detail":"cc_rejected_insufficient_amount","payment_method":{"id":"visa","type":"credit_card","installments":1}}]}
        }
        """;

    private const string PixResponse = """
        {
          "id":"ORD-PIX-1","external_reference":"oro-order-42","total_amount":"50.00",
          "status":"action_required","status_detail":"waiting_payment",
          "transactions":{"payments":[{"id":"PAY-PIX-1","amount":"50.00","status":"action_required","status_detail":"waiting_payment","date_of_expiration":"2026-09-17T15:30:00Z","payment_method":{"id":"pix","type":"bank_transfer","qr_code":"000201-copy-paste","qr_code_base64":"base64-image"}}]}
        }
        """;

    private static string PixResponseWithExpiration(string dateOfExpiration) =>
        """
        {
          "id":"ORD-PIX-1","external_reference":"oro-order-42","total_amount":"50.00",
          "status":"action_required","status_detail":"waiting_payment",
          "transactions":{"payments":[{"id":"PAY-PIX-1","amount":"50.00","status":"action_required","status_detail":"waiting_payment","date_of_expiration":"DATE_PLACEHOLDER","payment_method":{"id":"pix","type":"bank_transfer","qr_code":"000201-copy-paste","qr_code_base64":"base64-image"}}]}
        }
        """.Replace("DATE_PLACEHOLDER", dateOfExpiration);

    private const string PixResponseWithoutExpiration = """
        {
          "id":"ORD-PIX-1","external_reference":"oro-order-42","total_amount":"50.00",
          "status":"action_required","status_detail":"waiting_payment",
          "transactions":{"payments":[{"id":"PAY-PIX-1","amount":"50.00","status":"action_required","status_detail":"waiting_payment","payment_method":{"id":"pix","type":"bank_transfer","qr_code":"000201-copy-paste","qr_code_base64":"base64-image"}}]}
        }
        """;

    private const string CardResponse = """
        {
          "id":"ORD-CARD-1","external_reference":"oro-order-43","total_amount":"125.40",
          "status":"processed","status_detail":"accredited",
          "transactions":{"payments":[{"id":"PAY-CARD-1","amount":"125.40","status":"processed","status_detail":"accredited","payment_method":{"id":"master","type":"credit_card","installments":3}}]}
        }
        """;

    private const string RefundResponse = """
        {"id":"ORD-CARD-1","total_amount":"125.40","status":"refunded","status_detail":"refunded","transactions":{"refunds":[{"transaction_id":"PAY-CARD-1","amount":"125.40","status":"processed"}]}}
        """;

    // Sanitized copy of a real GET /v1/orders/{id} response for an approved card payment: same keys and
    // nesting Mercado Pago actually returns, with ids/token replaced by non-sensitive placeholders.
    private const string RealApprovedCardOrderResponse = """
        {
          "id":"ORD-CARD-REAL-1","type":"online","processing_mode":"automatic","external_reference":"oro-order-99","total_amount":"119.90","total_paid_amount":"119.90","country_code":"BRA","user_id":"0000000000","status":"processed","status_detail":"accredited","capture_mode":"automatic_async","currency":"BRL","created_date":"2026-09-18T13:39:40.473Z","last_updated_date":"2026-09-18T13:39:41.932Z","integration_data":{"application_id":"0000000000000000"},"config":{"online":{"transaction_security":{"validation":"never"}}},
          "transactions":{"payments":[{"id":"PAY-CARD-REAL-1","amount":"119.90","paid_amount":"119.90","reference_id":"sanitized-ref","status":"processed","status_detail":"accredited","payment_method":{"id":"visa","type":"credit_card","token":"sanitized-token-not-real","installments":1,"installment_amount":"119.90","transaction_security":{"validation":"never"}}}]}
        }
        """;

    private sealed class RecordingHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Idempotency-Key", out var values) ? values.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? Authorization, string? IdempotencyKey, string? Body);

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("The request timed out.");
    }
}
