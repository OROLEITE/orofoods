using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public sealed class MercadoPagoPointTerminalDiscoveryTests
{
    [Fact]
    public async Task ListTerminalsAsync_sends_only_read_only_list_request_and_maps_multiple_terminals()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "data": { "terminals": [
                { "id": "NEWLAND_N950__SERIAL-01", "store_id": "store-1", "pos_id": 17, "external_pos_id": "pos-ext-1", "operating_mode": "PDV", "ignored": "not exposed" },
                { "id": "PAX_A910__SERIAL-02", "store_id": "store-2", "pos_id": 29, "external_pos_id": "pos-ext-2", "operating_mode": "STANDALONE" }
              ] },
              "paging": { "total": 2, "offset": 0, "limit": 50 }
            }
            """);
        var sut = CreateService(handler);

        var terminals = await sut.ListTerminalsAsync();

        Assert.Equal(2, terminals.Count);
        Assert.Equal(new MercadoPagoPointTerminal("NEWLAND_N950__SERIAL-01", "store-1", "17", "pos-ext-1", "PDV"), terminals[0]);
        Assert.Equal(new MercadoPagoPointTerminal("PAX_A910__SERIAL-02", "store-2", "29", "pos-ext-2", "STANDALONE"), terminals[1]);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.mercadopago.com/terminals/v1/list?limit=50&offset=0", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer discovery-test-token", request.Authorization);
        Assert.DoesNotContain("/v1/orders", request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/terminals/v1/setup", request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListTerminalsAsync_returns_empty_list_when_account_has_no_terminals()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"data":{"terminals":[]},"paging":{"total":0,"offset":0,"limit":50}}""");
        var sut = CreateService(handler);

        var terminals = await sut.ListTerminalsAsync();

        Assert.Empty(terminals);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ListTerminalsAsync_surfaces_http_failure_without_provider_body(HttpStatusCode statusCode)
    {
        var handler = new RecordingHandler(statusCode, "sensitive provider error body");
        var sut = CreateService(handler);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(() => sut.ListTerminalsAsync());

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.DoesNotContain("sensitive provider error body", exception.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ListTerminalsAsync_propagates_timeout_without_retrying_or_mutating_terminal()
    {
        var handler = new TimeoutHandler();
        var sut = CreateService(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ListTerminalsAsync());

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.DoesNotContain("/v1/orders", handler.Path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/terminals/v1/setup", handler.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListTerminalsAsync_is_unavailable_outside_staging()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"terminals\":[]}}");
        var sut = CreateService(handler, "Production");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ListTerminalsAsync());

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ListTerminalsAsync_without_access_token_fails_before_http()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"terminals\":[]}}");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") };
        var sut = new MercadoPagoPointTerminalDiscovery(
            client,
            Options.Create(new MercadoPagoPointOptions()),
            new TestHostEnvironment("Staging"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ListTerminalsAsync());

        Assert.Equal("Credencial Mercado Pago Point não configurada.", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ListTerminalsAsync_sends_credentials_only_to_the_official_api_host()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"terminals\":[]}}");
        var sut = CreateService(handler, baseAddress: new Uri("https://attacker.invalid/"));

        await sut.ListTerminalsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("api.mercadopago.com", request.Uri.Host);
        Assert.Equal(HttpMethod.Get, request.Method);
    }

    private static IMercadoPagoPointTerminalDiscovery CreateService(
        HttpMessageHandler handler,
        string environment = "Staging",
        Uri? baseAddress = null)
    {
        var options = Options.Create(new MercadoPagoPointOptions { AccessToken = "discovery-test-token" });
        var host = new TestHostEnvironment(environment);
        var client = new HttpClient(handler) { BaseAddress = baseAddress ?? new Uri("https://api.mercadopago.com/") };
        return new MercadoPagoPointTerminalDiscovery(client, options, host);
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string Path { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            Path = request.RequestUri?.PathAndQuery ?? "";
            throw new TaskCanceledException("simulated timeout");
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Authorization);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
