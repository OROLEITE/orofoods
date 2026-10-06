using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Payments;

public sealed class MercadoPagoPointTerminalDiscovery(
    HttpClient httpClient,
    IOptions<MercadoPagoPointOptions> options,
    IHostEnvironment hostEnvironment) : IMercadoPagoPointTerminalDiscovery
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<MercadoPagoPointTerminal>> ListTerminalsAsync(CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsStaging())
        {
            throw new InvalidOperationException("A descoberta de terminais Mercado Pago Point está disponível somente no Staging.");
        }

        var accessToken = options.Value.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Credencial Mercado Pago Point não configurada.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("https://api.mercadopago.com/terminals/v1/list?limit=50&offset=0"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException(response.StatusCode);
        }

        MercadoPagoTerminalListResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<MercadoPagoTerminalListResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw new PaymentGatewayException(HttpStatusCode.BadGateway);
        }

        return payload?.Data?.Terminals?
            .Where(terminal => !string.IsNullOrWhiteSpace(terminal.Id))
            .Select(terminal => new MercadoPagoPointTerminal(
                terminal.Id!,
                terminal.StoreId,
                ReadIdentifier(terminal.PosId),
                terminal.ExternalPosId,
                terminal.OperatingMode))
            .ToArray() ?? [];
    }

    private static string? ReadIdentifier(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };

    private sealed class MercadoPagoTerminalListResponse
    {
        [JsonPropertyName("data")] public MercadoPagoTerminalListData? Data { get; init; }
    }

    private sealed class MercadoPagoTerminalListData
    {
        [JsonPropertyName("terminals")] public List<MercadoPagoTerminalResponse>? Terminals { get; init; }
    }

    private sealed class MercadoPagoTerminalResponse
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("store_id")] public string? StoreId { get; init; }
        [JsonPropertyName("pos_id")] public JsonElement PosId { get; init; }
        [JsonPropertyName("external_pos_id")] public string? ExternalPosId { get; init; }
        [JsonPropertyName("operating_mode")] public string? OperatingMode { get; init; }
    }
}
