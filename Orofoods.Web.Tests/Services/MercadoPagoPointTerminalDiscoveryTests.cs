using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task SetTerminalOperatingModeAsync_preflights_patches_only_authorized_terminal_and_verifies_mode()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, TerminalList("STANDALONE")),
            (HttpStatusCode.OK, "{\"terminals\":[{\"id\":\"NEWLAND_N950__N950NCD600484709\",\"operating_mode\":\"PDV\"}]}"),
            (HttpStatusCode.OK, TerminalList("PDV")));
        var sut = CreateService(handler);

        var result = await sut.SetTerminalOperatingModeAsync("NEWLAND_N950__N950NCD600484709", "PDV");

        Assert.Equal("STANDALONE", result.PreviousOperatingMode);
        Assert.Equal("PDV", result.OperatingMode);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://api.mercadopago.com/terminals/v1/list?limit=50&offset=0", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal(HttpMethod.Patch, handler.Requests[1].Method);
        Assert.Equal("https://api.mercadopago.com/terminals/v1/setup", handler.Requests[1].Uri.AbsoluteUri);
        Assert.Equal("application/json", handler.Requests[1].ContentType);
        using (var body = JsonDocument.Parse(handler.Requests[1].Body!))
        {
            var terminals = body.RootElement.GetProperty("terminals");
            var terminal = Assert.Single(terminals.EnumerateArray().ToArray());
            Assert.Equal(new[] { "id", "operating_mode" }, terminal.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.Equal("NEWLAND_N950__N950NCD600484709", terminal.GetProperty("id").GetString());
            Assert.Equal("PDV", terminal.GetProperty("operating_mode").GetString());
        }
        Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.Contains("/v1/orders", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("NEWLAND_N950__OTHER", "PDV")]
    [InlineData("NEWLAND_N950__N950NCD600484709", "STANDALONE")]
    [InlineData("NEWLAND_N950__N950NCD600484709", "UNDEFINED")]
    public async Task SetTerminalOperatingModeAsync_rejects_unapproved_terminal_or_mode_before_http(string terminalId, string mode)
    {
        var handler = new SequenceHandler();
        var sut = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetTerminalOperatingModeAsync(terminalId, mode));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SetTerminalOperatingModeAsync_does_not_patch_when_terminal_is_missing_or_not_standalone()
    {
        var missing = new SequenceHandler((HttpStatusCode.OK, "{\"data\":{\"terminals\":[]}}"));
        var missingSut = CreateService(missing);
        await Assert.ThrowsAsync<InvalidOperationException>(() => missingSut.SetTerminalOperatingModeAsync("NEWLAND_N950__N950NCD600484709", "PDV"));
        Assert.Single(missing.Requests);
        Assert.Equal(HttpMethod.Get, missing.Requests[0].Method);

        var alreadyConfigured = new SequenceHandler((HttpStatusCode.OK, TerminalList("PDV")));
        var configuredSut = CreateService(alreadyConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => configuredSut.SetTerminalOperatingModeAsync("NEWLAND_N950__N950NCD600484709", "PDV"));
        Assert.Single(alreadyConfigured.Requests);
        Assert.Equal(HttpMethod.Get, alreadyConfigured.Requests[0].Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SetTerminalOperatingModeAsync_does_not_retry_failed_patch(HttpStatusCode patchStatus)
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.OK, TerminalList("STANDALONE")),
            (patchStatus, "provider error"));
        var sut = CreateService(handler);

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => sut.SetTerminalOperatingModeAsync("NEWLAND_N950__N950NCD600484709", "PDV"));

        Assert.Equal(patchStatus, exception.StatusCode);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.Contains("/v1/orders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetTerminalOperatingModeAsync_does_not_mutate_outside_staging()
    {
        var handler = new SequenceHandler();
        var sut = CreateService(handler, "Production");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SetTerminalOperatingModeAsync("NEWLAND_N950__N950NCD600484709", "PDV"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SetTerminalOperatingModeAsync_timeout_does_not_retry_or_verify_after_patch_timeout()
    {
        var handler = new PatchTimeoutHandler();
        var sut = CreateService(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SetTerminalOperatingModeAsync("NEWLAND_N950__N950NCD600484709", "PDV"));

        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Patch }, handler.Methods);
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

    private static string TerminalList(string operatingMode) =>
        "{\"data\":{\"terminals\":[{\"id\":\"NEWLAND_N950__N950NCD600484709\",\"store_id\":\"88334226\",\"pos_id\":\"139110122\",\"operating_mode\":\"" + operatingMode + "\"}]}}";

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
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

    private sealed class SequenceHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new(responses);
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(),
                body, request.Content?.Headers.ContentType?.MediaType));
            var response = _responses.Dequeue();
            return new HttpResponseMessage(response.Status) { Content = new StringContent(response.Body) };
        }
    }

    private sealed class PatchTimeoutHandler : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            if (request.Method == HttpMethod.Patch)
            {
                return Task.FromException<HttpResponseMessage>(new TaskCanceledException("simulated timeout"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TerminalList("STANDALONE"))
            });
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body = null, string? ContentType = null);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
