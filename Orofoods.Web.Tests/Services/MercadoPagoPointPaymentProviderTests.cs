using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public sealed class MercadoPagoPointPaymentProviderTests
{
    [Fact]
    public async Task CreatePointOrder_sends_documented_payload_and_idempotency_and_returns_order_ids()
    {
        var handler = new RecordingHandler(CreatedOrderResponse);
        var provider = CreateProvider(handler);

        var result = await provider.CreateTerminalPaymentAsync(TestRequest());

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.mercadopago.com/v1/orders"), request.Uri);
        Assert.Equal("Bearer test-access-token", request.Authorization);
        Assert.Equal("attempt-point-1", request.IdempotencyKey);
        using var json = JsonDocument.Parse(request.Body!);
        Assert.Equal("point", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("oro-order-42-attempt-20", json.RootElement.GetProperty("external_reference").GetString());
        Assert.Equal("125.50", json.RootElement.GetProperty("transactions").GetProperty("payments")[0].GetProperty("amount").GetString());
        Assert.Equal("NEWLAND_N950__SBX0000001", json.RootElement.GetProperty("config").GetProperty("point").GetProperty("terminal_id").GetString());
        Assert.DoesNotContain("store_id", request.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pos_id", request.Body!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ORD-POINT-1", ReadProperty<string>(result, "GatewayOrderId"));
        Assert.Equal("PAY-POINT-1", ReadProperty<string>(result, "GatewayPaymentId"));
        Assert.Equal(PaymentStatus.Pending, result.PaymentStatus);
    }

    [Fact]
    public async Task Create_response_without_aggregate_total_uses_the_single_documented_transaction_amount()
    {
        const string response = """
            {
              "id":"ORD-POINT-1",
              "type":"point",
              "external_reference":"oro-order-42-attempt-20",
              "status":"created",
              "transactions":{"payments":[{
                "id":"PAY-POINT-1",
                "amount":"125.50",
                "status":"created"
              }]}
            }
            """;
        var provider = CreateProvider(new RecordingHandler(response));

        var result = await provider.CreateTerminalPaymentAsync(TestRequest());

        Assert.Equal(PaymentStatus.Pending, result.PaymentStatus);
        Assert.Equal(125.50m, result.TotalAmount);
        Assert.Single(result.Financials!.Transactions);
        Assert.Equal(125.50m, result.Financials.Transactions[0].Amount);
    }

    [Theory]
    [InlineData("created", "created", "Pending")]
    [InlineData("pending", "pending", "Pending")]
    [InlineData("at_terminal", "at_terminal", "Processing")]
    [InlineData("processed", "accredited", "Approved")]
    [InlineData("failed", "insufficient_amount", "Rejected")]
    [InlineData("canceled", "canceled_on_terminal", "Cancelled")]
    [InlineData("expired", "expired", "Expired")]
    [InlineData("action_required", "check_on_terminal", "ActionRequired")]
    [InlineData("refunded", "refunded", "Refunded")]
    public async Task GetPointOrder_maps_documented_statuses_without_inventing_approval(string status, string detail, string expected)
    {
        var handler = new RecordingHandler(OrderResponse(status, detail));
        var provider = CreateProvider(handler);

        var result = await provider.GetPaymentStatusAsync("ORD-POINT-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://api.mercadopago.com/v1/orders/ORD-POINT-1"), request.Uri);
        Assert.Equal(expected, result.PaymentStatus?.ToString());
        Assert.Equal("ORD-POINT-1", ReadProperty<string>(result, "GatewayOrderId"));
        Assert.Equal("oro-order-42-attempt-20", ReadProperty<string>(result, "ExternalReference"));
        Assert.Equal(125.50m, ReadProperty<decimal>(result, "TotalAmount"));
    }

    [Fact]
    public async Task Processed_without_accredited_payment_is_not_approved()
    {
        var provider = CreateProvider(new RecordingHandler(OrderResponse("processed", "pending", paymentStatus: "processing")));

        var result = await provider.GetPaymentStatusAsync("ORD-POINT-1");

        Assert.NotEqual(PaymentStatus.Approved, result.PaymentStatus);
    }

    [Fact]
    public async Task GetPointOrder_maps_order_and_transaction_paid_amount_fields()
    {
        const string response = """
            {
              "id":"ORD-POINT-1",
              "type":"point",
              "external_reference":"oro-order-42-attempt-20",
              "total_amount":"125.50",
              "total_paid_amount":"125.50",
              "status":"processed",
              "status_detail":"accredited",
              "transactions":{"payments":[{
                "id":"PAY-POINT-1",
                "amount":"125.50",
                "paid_amount":"125.50",
                "status":"processed",
                "status_detail":"accredited"
              }]}
            }
            """;
        var provider = CreateProvider(new RecordingHandler(response));

        var result = await provider.GetPaymentStatusAsync("ORD-POINT-1");

        Assert.Equal(PaymentStatus.Approved, result.PaymentStatus);
        Assert.NotNull(result.Financials);
        Assert.Equal(125.50m, result.Financials.TotalPaidAmount);
        Assert.True(result.Financials.TotalPaidAmountPresent);
        var transaction = Assert.Single(result.Financials.Transactions);
        Assert.Equal("PAY-POINT-1", transaction.Id);
        Assert.Equal("processed", transaction.Status);
        Assert.Equal(125.50m, transaction.Amount);
        Assert.Equal(125.50m, transaction.PaidAmount);
    }

    [Fact]
    public async Task GetPointOrder_preserves_explicit_null_financial_fields_as_invalid()
    {
        const string response = """
            {
              "id":"ORD-POINT-1",
              "type":"point",
              "external_reference":"oro-order-42-attempt-20",
              "total_amount":"125.50",
              "total_paid_amount":null,
              "status":"processed",
              "status_detail":"accredited",
              "transactions":{"payments":[{
                "id":"PAY-POINT-1",
                "amount":"125.50",
                "paid_amount":null,
                "status":"processed",
                "status_detail":"accredited"
              }]}
            }
            """;
        var provider = CreateProvider(new RecordingHandler(response));

        var result = await provider.GetPaymentStatusAsync("ORD-POINT-1");

        Assert.NotNull(result.Financials);
        Assert.True(result.Financials.TotalPaidAmountPresent);
        Assert.True(result.Financials.TotalPaidAmountInvalid);
        var transaction = Assert.Single(result.Financials.Transactions);
        Assert.True(transaction.PaidAmountPresent);
        Assert.True(transaction.PaidAmountInvalid);
    }

    [Fact]
    public async Task GetPointOrder_with_multiple_payment_transactions_does_not_select_first_for_approval()
    {
        const string response = """
            {
              "id":"ORD-POINT-1",
              "type":"point",
              "external_reference":"oro-order-42-attempt-20",
              "total_amount":"125.50",
              "status":"processed",
              "status_detail":"accredited",
              "transactions":{"payments":[
                {"id":"PAY-1","amount":"125.50","paid_amount":"125.50","status":"processed","status_detail":"accredited"},
                {"id":"PAY-2","amount":"125.50","paid_amount":"125.50","status":"processed","status_detail":"accredited"}
              ]}
            }
            """;
        var provider = CreateProvider(new RecordingHandler(response));

        var result = await provider.GetPaymentStatusAsync("ORD-POINT-1");

        Assert.NotEqual(PaymentStatus.Approved, result.PaymentStatus);
        Assert.Equal(2, result.Financials?.Transactions.Count);
    }

    [Fact]
    public async Task Unknown_status_fails_closed_without_mapping_to_a_payment_status()
    {
        var provider = CreateProvider(new RecordingHandler(OrderResponse("future_provider_status", "unknown")));

        var result = await provider.GetPaymentStatusAsync("ORD-POINT-1");

        Assert.Null(result.PaymentStatus);
        Assert.Equal("UNKNOWN_POINT_STATUS", result.ErrorCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Provider_refuses_real_environments_before_http(string environmentName)
    {
        var handler = new RecordingHandler(CreatedOrderResponse);
        var provider = CreateProvider(handler, environmentName: environmentName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateTerminalPaymentAsync(TestRequest()));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Get")]
    [InlineData("Cancel")]
    public async Task Provider_blocks_all_point_payment_operations_in_staging(string operation)
    {
        var handler = new RecordingHandler(CreatedOrderResponse);
        var provider = CreateProvider(handler, environmentName: "Staging");

        async Task Act()
        {
            switch (operation)
            {
                case "Get": await provider.GetPaymentStatusAsync("ORDER-1"); break;
                case "Cancel": await provider.CancelPendingPaymentAsync("ORDER-1"); break;
                default: await provider.CreateTerminalPaymentAsync(TestRequest()); break;
            }
        }

        await Assert.ThrowsAsync<InvalidOperationException>(Act);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Provider_without_access_token_fails_closed_before_http()
    {
        var handler = new RecordingHandler(CreatedOrderResponse);
        var provider = CreateProvider(handler, accessToken: "");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.CreateTerminalPaymentAsync(TestRequest()));

        Assert.Contains("Credenciais de teste", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Test_provider_refuses_non_virtual_device_before_http()
    {
        var handler = new RecordingHandler(CreatedOrderResponse);
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateTerminalPaymentAsync(TestRequest() with { DeviceId = "PHYSICAL-DEVICE" }));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Cancel_uses_the_point_order_cancel_endpoint()
    {
        var handler = new RecordingHandler("", HttpStatusCode.NoContent);
        var provider = CreateProvider(handler);

        await provider.CancelPendingPaymentAsync("ORD-POINT-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.mercadopago.com/v1/orders/ORD-POINT-1/cancel"), request.Uri);
    }

    [Fact]
    public async Task Gateway_error_does_not_expose_response_body_or_access_token()
    {
        var provider = CreateProvider(new RecordingHandler("{\"message\":\"test-access-token private detail\"}", HttpStatusCode.BadRequest));

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => provider.CreateTerminalPaymentAsync(TestRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.DoesNotContain("test-access-token", exception.Message);
        Assert.DoesNotContain("private detail", exception.Message);
    }

    [Fact]
    public async Task Malformed_success_response_is_reported_as_sanitized_gateway_error()
    {
        var provider = CreateProvider(new RecordingHandler("not-json"));

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => provider.CreateTerminalPaymentAsync(TestRequest()));

        Assert.DoesNotContain("not-json", exception.Message);
    }

    [Fact]
    public async Task Http_timeout_propagates_without_retrying_with_a_new_request()
    {
        var handler = new TimeoutHandler();
        var provider = CreateProvider(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CreateTerminalPaymentAsync(TestRequest()));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Caller_cancellation_is_forwarded_to_the_http_request()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new CancellationObservingHandler(cancellation);
        var provider = CreateProvider(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CreateTerminalPaymentAsync(TestRequest(), cancellation.Token));

        Assert.True(handler.CancellationObserved);
    }

    private static IPointPaymentProvider CreateProvider(
        HttpMessageHandler handler,
        string environmentName = "Test",
        string accessToken = "test-access-token")
    {
        var providerType = typeof(IPointPaymentProvider).Assembly.GetType("Orofoods.Web.Services.Payments.MercadoPagoPointPaymentProvider");
        Assert.NotNull(providerType);
        var options = new MercadoPagoPointOptions { Enabled = true };
        SetOption(options, "Environment", "Test");
        SetOption(options, "AccessToken", accessToken);
        SetOption(options, "PoiType", "NEWLAND_N950");
        SetOption(options, "BaseAddress", new Uri("https://api.mercadopago.com/"));
        var hostEnvironment = new TestHostEnvironment(environmentName);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") };
        var instance = Activator.CreateInstance(providerType!, client, Options.Create(options), hostEnvironment);
        return Assert.IsAssignableFrom<IPointPaymentProvider>(instance);
    }

    private static void SetOption(MercadoPagoPointOptions options, string name, object value) =>
        typeof(MercadoPagoPointOptions).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.SetValue(options, value);

    private static T? ReadProperty<T>(object instance, string name) =>
        (T?)instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);

    private static PointPaymentRequest TestRequest() => new(
        OrderId: 42,
        PaymentId: 20,
        AssignmentId: 30,
        Amount: 125.50m,
        DeviceId: "SBX0000001",
        StoreId: "store-local-only",
        PosId: "pos-local-only",
        IdempotencyKey: "attempt-point-1");

    private static string OrderResponse(string status, string detail, string? paymentStatus = null) => $$"""
        {
          "id":"ORD-POINT-1",
          "type":"point",
          "external_reference":"oro-order-42-attempt-20",
          "total_amount":"125.50",
          "status":"{{status}}",
          "status_detail":"{{detail}}",
          "transactions":{"payments":[{"id":"PAY-POINT-1","amount":"125.50","status":"{{paymentStatus ?? status}}","status_detail":"{{detail}}"}]}
        }
        """;

    private const string CreatedOrderResponse = """
        {"id":"ORD-POINT-1","type":"point","external_reference":"oro-order-42-attempt-20","total_amount":"125.50","status":"created","status_detail":"created","transactions":{"payments":[{"id":"PAY-POINT-1","amount":"125.50","status":"created","status_detail":"created"}]}}
        """;

    private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Idempotency-Key", out var values) ? values.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new TaskCanceledException("simulated timeout");
        }
    }

    private sealed class CancellationObservingHandler(CancellationTokenSource cancellation) : HttpMessageHandler
    {
        public bool CancellationObserved { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CancellationObserved = cancellationToken.CanBeCanceled;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? IdempotencyKey, string? Body);

    private sealed class TestHostEnvironment(string environmentName) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
