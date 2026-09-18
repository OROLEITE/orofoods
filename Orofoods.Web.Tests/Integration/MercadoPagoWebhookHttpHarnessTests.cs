using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Integration;

/// <summary>
/// End-to-end local harness: real HTTP request through ASP.NET Core routing, the real
/// MercadoPagoWebhooksController, the real MercadoPagoWebhookSignatureValidator and the real
/// PaymentOrchestrationService. Only the Mercado Pago HTTP gateway is faked and the database is an
/// isolated in-memory SQLite instance. Nothing here ever reaches api.mercadopago.com, Postgres, or WMC.
/// </summary>
public class MercadoPagoWebhookHttpHarnessTests
{
    private const string HarnessWebhookSecret = "harness-only-secret-not-committed-anywhere";

    [Fact]
    public async Task First_delivery_reconciles_pending_to_approved_and_duplicate_delivery_is_idempotent()
    {
        await using var harness = await Harness.CreateAsync(HarnessWebhookSecret);
        var payment = await harness.SeedPendingPaymentAsync("HARNESS-ORDER-77");
        harness.Gateway.Next = Harness.ApprovedOrder("HARNESS-ORDER-77", payment.Amount, payment.ExternalReference);

        var first = await harness.SendNotificationAsync("HARNESS-ORDER-77", "harness-request-1", HarnessWebhookSecret);
        Assert.Equal(HttpStatusCode.OK, first);

        var afterFirst = await harness.ReloadPaymentAsync(payment.Id);
        Assert.Equal(PaymentStatus.Approved, afterFirst.Status);
        Assert.NotNull(afterFirst.PaidAt);
        // SQLite (test-only storage) does not round-trip DateTimeKind; the value written was UTC (TimeProvider.System.GetUtcNow().UtcDateTime).
        var paidAtUtc = DateTime.SpecifyKind(afterFirst.PaidAt!.Value, DateTimeKind.Utc);
        Assert.True((DateTime.UtcNow - paidAtUtc).Duration() < TimeSpan.FromMinutes(1), "PaidAt should be a recent UTC timestamp.");
        Assert.Equal(1, harness.Gateway.GetOrderCalls);
        Assert.Equal(1, await harness.CountPaymentsAsync());

        // Exactly the same notification, replayed.
        var second = await harness.SendNotificationAsync("HARNESS-ORDER-77", "harness-request-1", HarnessWebhookSecret);
        Assert.Equal(HttpStatusCode.OK, second);

        var afterSecond = await harness.ReloadPaymentAsync(payment.Id);
        Assert.Equal(PaymentStatus.Approved, afterSecond.Status);
        Assert.Equal(afterFirst.PaidAt, afterSecond.PaidAt);
        Assert.Equal(2, harness.Gateway.GetOrderCalls); // each delivery re-verifies with the gateway; no duplicated *effect*.
        Assert.Equal(1, await harness.CountPaymentsAsync());
    }

    [Fact]
    public async Task Card_delivery_reconciles_pending_to_approved_over_real_http()
    {
        await using var harness = await Harness.CreateAsync(HarnessWebhookSecret);
        var payment = await harness.SeedPendingPaymentAsync("HARNESS-ORDER-99", PaymentMethodType.CreditCard);
        harness.Gateway.Next = Harness.ApprovedOrder("HARNESS-ORDER-99", payment.Amount, payment.ExternalReference);

        var status = await harness.SendNotificationAsync("HARNESS-ORDER-99", "harness-request-card-1", HarnessWebhookSecret);

        Assert.Equal(HttpStatusCode.OK, status);
        var afterFirst = await harness.ReloadPaymentAsync(payment.Id);
        Assert.Equal(PaymentStatus.Approved, afterFirst.Status);
        Assert.NotNull(afterFirst.PaidAt);
        Assert.Equal(1, harness.Gateway.GetOrderCalls);
    }

    [Fact]
    public async Task Tampered_signature_is_rejected_and_the_gateway_is_never_called()
    {
        await using var harness = await Harness.CreateAsync(HarnessWebhookSecret);
        var payment = await harness.SeedPendingPaymentAsync("HARNESS-ORDER-88");
        harness.Gateway.Next = Harness.ApprovedOrder("HARNESS-ORDER-88", payment.Amount, payment.ExternalReference);

        var status = await harness.SendNotificationAsync("HARNESS-ORDER-88", "harness-request-2", signingSecret: "wrong-secret-used-on-purpose");

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal(0, harness.Gateway.GetOrderCalls);
        var stillPending = await harness.ReloadPaymentAsync(payment.Id);
        Assert.Equal(PaymentStatus.Pending, stillPending.Status);
        Assert.Null(stillPending.PaidAt);
    }

    private sealed class RecordingFakeGateway : IPaymentGateway
    {
        public PaymentGatewayOrder? Next { get; set; }
        public int GetOrderCalls { get; private set; }

        public Task<PaymentGatewayOrder> CreatePixAsync(CreatePixPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by this webhook-only harness.");

        public Task<PaymentGatewayOrder> CreateCreditCardPaymentAsync(CreateCreditCardPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by this webhook-only harness.");

        public Task<PaymentGatewayOrder> GetOrderAsync(string gatewayOrderId, CancellationToken cancellationToken = default)
        {
            GetOrderCalls++;
            return Task.FromResult(Next ?? throw new InvalidOperationException("Harness gateway response was not configured."));
        }

        public Task<PaymentGatewayOrder> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by this webhook-only harness.");
    }

    /// <summary>Minimal, isolated ASP.NET Core test host exposing only the webhook route with a real signature validator and a fake gateway.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IHost _host;
        private readonly TestServer _server;
        private readonly HttpClient _client;

        public RecordingFakeGateway Gateway { get; }

        private Harness(SqliteConnection connection, IHost host, TestServer server, HttpClient client, RecordingFakeGateway gateway)
        {
            _connection = connection;
            _host = host;
            _server = server;
            _client = client;
            Gateway = gateway;
        }

        public static async Task<Harness> CreateAsync(string webhookSecret)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using (var seedContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options))
            {
                await seedContext.Database.EnsureCreatedAsync();
            }

            var gateway = new RecordingFakeGateway();

            var host = await new HostBuilder()
                .ConfigureWebHost(webHostBuilder => webHostBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddControllers().AddApplicationPart(typeof(MercadoPagoWebhooksController).Assembly);
                        services.AddSingleton(TimeProvider.System);
                        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
                        services.Configure<MercadoPagoOptions>(options => options.WebhookSecret = webhookSecret);
                        services.Configure<PaymentEligibilityOptions>(_ => { });
                        services.AddScoped<IPaymentEligibilityService, PaymentEligibilityService>();
                        services.AddSingleton<IPaymentGateway>(gateway);
                        services.AddScoped<IPaymentApprovalHandler, NoOpPaymentApprovalHandler>();
                        services.AddScoped<IMercadoPagoWebhookSignatureValidator, MercadoPagoWebhookSignatureValidator>();
                        services.AddScoped<PaymentOrchestrationService>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .StartAsync();

            var server = host.GetTestServer();
            return new Harness(connection, host, server, server.CreateClient(), gateway);
        }

        public async Task<Payment> SeedPendingPaymentAsync(string gatewayOrderId, PaymentMethodType method = PaymentMethodType.Pix)
        {
            using var scope = _server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var methodCode = method == PaymentMethodType.CreditCard ? "CREDIT_CARD" : "PIX";
            var customer = new Customer { LegalName = "Harness Ltda", TradeName = "Harness", Cnpj = Guid.NewGuid().ToString("N")[..14], Email = "harness@testuser.com" };
            var term = new PaymentTerm { Code = methodCode, Name = methodCode, DaysUntilDue = 0, IsActive = true };
            var order = new Order { Customer = customer, PaymentTerm = term, PaymentMethod = methodCode, Total = 77.50m, Status = OrderStatus.Received };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            var payment = new Payment
            {
                OrderId = order.Id,
                CustomerId = customer.Id,
                PaymentMethod = methodCode,
                Method = method,
                Amount = order.Total,
                Status = PaymentStatus.Pending,
                Gateway = "MercadoPago",
                GatewayOrderId = gatewayOrderId,
                GatewayPaymentId = $"{gatewayOrderId}-PAYMENT",
                ExternalReference = $"orofoods-order-{order.Id}",
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            return payment;
        }

        public async Task<Payment> ReloadPaymentAsync(int paymentId)
        {
            using var scope = _server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Payments.AsNoTracking().SingleAsync(payment => payment.Id == paymentId);
        }

        public async Task<int> CountPaymentsAsync()
        {
            using var scope = _server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Payments.CountAsync();
        }

        public async Task<HttpStatusCode> SendNotificationAsync(string gatewayOrderId, string requestId, string signingSecret)
        {
            var timestampMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var manifest = $"id:{gatewayOrderId};request-id:{requestId};ts:{timestampMilliseconds};";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
            var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest)));

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/webhooks/mercadopago?data.id={gatewayOrderId}&type=order")
            {
                Content = new ByteArrayContent([])
            };
            request.Headers.Add("x-signature", $"ts={timestampMilliseconds},v1={hash}");
            request.Headers.Add("x-request-id", requestId);

            using var response = await _client.SendAsync(request);
            return response.StatusCode;
        }

        public static PaymentGatewayOrder ApprovedOrder(string gatewayOrderId, decimal amount, string? externalReference) =>
            new(gatewayOrderId, $"{gatewayOrderId}-PAYMENT", externalReference, amount, PaymentStatus.Approved, null, null, null, null, null);

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            _server.Dispose();
            _host.Dispose();
            await _connection.DisposeAsync();
        }
    }
}
