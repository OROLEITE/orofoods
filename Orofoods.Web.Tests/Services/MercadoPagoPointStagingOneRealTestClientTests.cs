using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public sealed class MercadoPagoPointStagingOneRealTestClientTests
{
    private const string OrderResponse = """
        {"id":"ORDER-TEST-1","type":"point","external_reference":"oro_point_staging_one_real_test","status":"created"}
        """;

    [Fact]
    public async Task Start_sends_only_fixed_terminal_amount_external_reference_and_idempotency_key()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(OrderResponse)
        });
        var client = CreateClient(handler);

        var result = await client.StartAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://api.mercadopago.com/v1/orders"), request.Uri);
        Assert.Equal("Bearer synthetic-access-token", request.Authorization);
        Assert.Equal(MercadoPagoPointStagingOneRealTestClient.FixedIdempotencyKey, request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("point", body.RootElement.GetProperty("type").GetString());
        Assert.Equal(MercadoPagoPointStagingOneRealTestClient.FixedExternalReference,
            body.RootElement.GetProperty("external_reference").GetString());
        Assert.Equal("1.00", body.RootElement.GetProperty("transactions").GetProperty("payments")[0].GetProperty("amount").GetString());
        Assert.Equal("NEWLAND_N950__N950NCD600484709",
            body.RootElement.GetProperty("config").GetProperty("point").GetProperty("terminal_id").GetString());
        Assert.Equal("ORDER-TEST-1", result.OrderId);
        Assert.Equal("created", result.Status);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Timeout_consumes_the_single_attempt_and_does_not_automatically_resend()
    {
        var handler = new RecordingHandler(_ => throw new OperationCanceledException("simulated timeout"));
        var client = CreateClient(handler);

        var first = await client.StartAsync();
        var second = await client.StartAsync();

        Assert.False(first.Succeeded);
        Assert.Contains("Não envie novamente", first.Message, StringComparison.Ordinal);
        Assert.False(second.Succeeded);
        Assert.Single(handler.Requests);
        Assert.Null(second.OrderId);
        Assert.Null(second.Status);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Test", true)]
    [InlineData("Staging", false)]
    public async Task Create_is_rejected_without_http_unless_staging_and_flag_enabled(string environment, bool enabled)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(OrderResponse)
        });
        var client = CreateClient(handler, environment, enabled);

        var result = await client.StartAsync();

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Status_query_is_read_only_and_returns_only_matching_test_order()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OrderResponse.Replace("created", "at_terminal", StringComparison.Ordinal))
        });
        var client = CreateClient(handler);

        var result = await client.GetStatusAsync("ORDER-TEST-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://api.mercadopago.com/v1/orders/ORDER-TEST-1"), request.Uri);
        Assert.Null(request.IdempotencyKey);
        Assert.Equal("ORDER-TEST-1", result.OrderId);
        Assert.Equal("at_terminal", result.Status);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Status_query_rejects_an_order_with_a_different_external_reference()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OrderResponse.Replace(MercadoPagoPointStagingOneRealTestClient.FixedExternalReference, "other_order", StringComparison.Ordinal))
        });
        var client = CreateClient(handler);

        var result = await client.GetStatusAsync("ORDER-OTHER");

        Assert.False(result.Succeeded);
        Assert.Null(result.OrderId);
        Assert.Null(result.Status);
    }

    [Fact]
    public void Staging_client_is_only_injected_into_the_administrator_terminal_controller()
    {
        var consumers = typeof(MercadoPagoPointStagingOneRealTestClient).Assembly.GetTypes()
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IPointStagingOneRealTestClient)))
                .Select(_ => type))
            .ToArray();

        Assert.Equal(typeof(PaymentTerminalsController), Assert.Single(consumers));
    }

    private static MercadoPagoPointStagingOneRealTestClient CreateClient(
        RecordingHandler handler,
        string environment = "Staging",
        bool enabled = true)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") };
        return new MercadoPagoPointStagingOneRealTestClient(
            httpClient,
            Options.Create(new MercadoPagoPointOptions { AccessToken = "synthetic-access-token" }),
            Options.Create(new PointStagingOneRealTestOptions { Enabled = enabled }),
            new TestHostEnvironment(environment),
            new PointStagingOneRealTestAttemptGate());
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<RequestRecord> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RequestRecord(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Idempotency-Key", out var values) ? values.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private sealed record RequestRecord(HttpMethod Method, Uri Uri, string? Authorization, string? IdempotencyKey, string? Body);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
