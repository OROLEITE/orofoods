using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Payments;

public sealed class PointStagingOneRealTestOptions
{
    public const string ConfigurationKey = "Payments:PointStagingOneRealTestEnabled";

    public bool Enabled { get; set; }
}

public interface IPointStagingOneRealTestClient
{
    bool AttemptStarted { get; }
    Task<PointStagingOneRealTestResult> StartAsync(CancellationToken cancellationToken = default);
    Task<PointStagingOneRealTestResult> GetStatusAsync(string orderId, CancellationToken cancellationToken = default);
}

public sealed record PointStagingOneRealTestResult(
    string? OrderId,
    string? Status,
    string Message,
    bool Succeeded);

/// <summary>
/// In-process guard for the one explicitly enabled Staging Point test. It remains consumed after
/// any POST attempt, including timeouts, so the application never automatically submits again.
/// The fixed Mercado Pago idempotency key is shared across app instances and never replaced by a new key.
/// </summary>
public sealed class PointStagingOneRealTestAttemptGate
{
    private int _started;

    public bool AttemptStarted => Volatile.Read(ref _started) != 0;

    public bool TryStart() => Interlocked.CompareExchange(ref _started, 1, 0) == 0;
}

public sealed class MercadoPagoPointStagingOneRealTestClient(
    HttpClient httpClient,
    IOptions<MercadoPagoPointOptions> pointOptions,
    IOptions<PointStagingOneRealTestOptions> testOptions,
    IHostEnvironment hostEnvironment,
    PointStagingOneRealTestAttemptGate attemptGate) : IPointStagingOneRealTestClient
{
    public bool AttemptStarted => attemptGate.AttemptStarted;

    public const string AuthorizedTerminalId = "NEWLAND_N950__N950NCD600484709";
    public const string FixedAmount = "1.00";
    public const string FixedIdempotencyKey = "d3d97748-4252-4fc4-a4dd-560a05c03e8d";
    public const string FixedExternalReference = "oro_point_staging_one_real_test";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PointStagingOneRealTestResult> StartAsync(CancellationToken cancellationToken = default)
    {
        var unavailable = ValidateConfiguration();
        if (unavailable is not null) return unavailable;
        if (!attemptGate.TryStart())
        {
            return Failure("A tentativa única já foi iniciada. Não envie novamente; consulte o status da order.");
        }

        var payload = new PointOrderRequest(
            "point",
            FixedExternalReference,
            new PointTransactionsRequest([new PointPaymentRequest(FixedAmount)]),
            new PointConfigRequest(new PointTerminalConfigRequest(AuthorizedTerminalId)));
        using var request = CreateRequest(HttpMethod.Post, "v1/orders", FixedIdempotencyKey);
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failure("O Mercado Pago não confirmou a criação da order. Não envie novamente; consulte o status se tiver o order_id.");
            }

            var order = await ReadOrderAsync(response, cancellationToken);
            return IsThisTestOrder(order, expectedOrderId: null)
                ? new PointStagingOneRealTestResult(order.Id, order.Status, "Order enviada ao terminal; confira a maquininha.", true)
                : Failure("A resposta não confirmou a order do teste. Não envie novamente.");
        }
        catch (HttpRequestException)
        {
            return Failure("Não foi possível confirmar a resposta do Mercado Pago. Não envie novamente; consulte o status se tiver o order_id.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("A resposta do Mercado Pago excedeu o tempo limite. Não envie novamente; consulte o status se tiver o order_id.");
        }
        catch (JsonException)
        {
            return Failure("A resposta do Mercado Pago não pôde ser confirmada. Não envie novamente; consulte o status se tiver o order_id.");
        }
    }

    public async Task<PointStagingOneRealTestResult> GetStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var unavailable = ValidateConfiguration();
        if (unavailable is not null) return unavailable;
        if (string.IsNullOrWhiteSpace(orderId) || orderId.Length > 100
            || orderId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
        {
            return Failure("Informe um order_id válido para consultar o status.");
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"v1/orders/{Uri.EscapeDataString(orderId)}", null);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failure("Não foi possível consultar o status da order informada.");
            }

            var order = await ReadOrderAsync(response, cancellationToken);
            return IsThisTestOrder(order, orderId)
                ? new PointStagingOneRealTestResult(order.Id, order.Status, "Status consultado no Mercado Pago.", true)
                : Failure("A order consultada não corresponde ao teste Point autorizado.");
        }
        catch (HttpRequestException)
        {
            return Failure("Não foi possível consultar o status da order informada.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("A consulta do status excedeu o tempo limite.");
        }
        catch (JsonException)
        {
            return Failure("A resposta de status não pôde ser confirmada.");
        }
    }

    private PointStagingOneRealTestResult? ValidateConfiguration()
    {
        if (!hostEnvironment.IsStaging()) return Failure("O teste Point está disponível somente no Staging.");
        if (!testOptions.Value.Enabled) return Failure("O teste real Point está desabilitado.");
        if (string.IsNullOrWhiteSpace(pointOptions.Value.AccessToken)) return Failure("A credencial Mercado Pago não está configurada.");
        if (!IsMercadoPagoApiAddress(pointOptions.Value.BaseAddress)
            || !IsMercadoPagoApiAddress(httpClient.BaseAddress))
        {
            return Failure("O endereço da API Mercado Pago não está autorizado para este teste.");
        }

        return null;
    }

    private static bool IsMercadoPagoApiAddress(Uri? address) =>
        address is not null
        && address.Scheme == Uri.UriSchemeHttps
        && string.Equals(address.Host, "api.mercadopago.com", StringComparison.OrdinalIgnoreCase)
        && address.IsDefaultPort
        && address.AbsolutePath == "/"
        && string.IsNullOrEmpty(address.Query)
        && string.IsNullOrEmpty(address.Fragment);

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pointOptions.Value.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (idempotencyKey is not null) request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return request;
    }

    private static async Task<PointOrderResponse> ReadOrderAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<PointOrderResponse>(stream, JsonOptions, cancellationToken)
            ?? throw new JsonException("Empty Mercado Pago order response.");
    }

    private static bool IsThisTestOrder(PointOrderResponse order, string? expectedOrderId) =>
        !string.IsNullOrWhiteSpace(order.Id)
        && !string.IsNullOrWhiteSpace(order.Status)
        && (expectedOrderId is null || string.Equals(order.Id, expectedOrderId, StringComparison.Ordinal))
        && string.Equals(order.Type, "point", StringComparison.OrdinalIgnoreCase)
        && string.Equals(order.ExternalReference, FixedExternalReference, StringComparison.Ordinal);

    private static PointStagingOneRealTestResult Failure(string message) => new(null, null, message, false);

    private sealed record PointOrderRequest(
        string Type,
        string ExternalReference,
        PointTransactionsRequest Transactions,
        PointConfigRequest Config);
    private sealed record PointTransactionsRequest(IReadOnlyList<PointPaymentRequest> Payments);
    private sealed record PointPaymentRequest(string Amount);
    private sealed record PointConfigRequest(PointTerminalConfigRequest Point);
    private sealed record PointTerminalConfigRequest(string TerminalId);

    private sealed class PointOrderResponse
    {
        public string Id { get; init; } = "";
        public string? Type { get; init; }
        public string? ExternalReference { get; init; }
        public string? Status { get; init; }
    }
}
