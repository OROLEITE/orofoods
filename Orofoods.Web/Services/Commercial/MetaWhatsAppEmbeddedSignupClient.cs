using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Commercial;

public interface IMetaWhatsAppEmbeddedSignupClient
{
    Task<string> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetaWhatsAppPhoneNumber>> GetPhoneNumbersAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default);
    Task SubscribeAppAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default);
    Task RequestCoexistenceSyncAsync(string phoneNumberId, string accessToken, string syncType, CancellationToken cancellationToken = default);
}

public sealed record MetaWhatsAppPhoneNumber(string Id, string DisplayPhoneNumber);

public sealed class MetaWhatsAppEmbeddedSignupClient(
    HttpClient httpClient,
    IOptions<WhatsAppBusinessOptions> options) : IMetaWhatsAppEmbeddedSignupClient
{
    private static readonly HashSet<string> SupportedSyncTypes = ["history"];

    public async Task<string> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(options.Value.EmbeddedSignupAppId) ||
            string.IsNullOrWhiteSpace(options.Value.AppSecret))
            throw new InvalidOperationException("A autorização do WhatsApp Business não está configurada.");

        var uri = $"{Version}/oauth/access_token?client_id={Uri.EscapeDataString(options.Value.EmbeddedSignupAppId)}&client_secret={Uri.EscapeDataString(options.Value.AppSecret)}&code={Uri.EscapeDataString(code)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadResponseAsync<TokenExchangeResponse>(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new InvalidOperationException("A Meta não retornou uma autorização WhatsApp válida.");

        return payload.AccessToken;
    }

    public async Task<IReadOnlyList<MetaWhatsAppPhoneNumber>> GetPhoneNumbersAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"{Version}/{Uri.EscapeDataString(businessAccountId)}/phone_numbers", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadResponseAsync<PhoneNumbersResponse>(response, cancellationToken);
        return payload.Data?.Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.DisplayPhoneNumber))
            .Select(x => new MetaWhatsAppPhoneNumber(x.Id!, x.DisplayPhoneNumber!)).ToArray() ?? [];
    }

    public async Task SubscribeAppAsync(string businessAccountId, string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{Version}/{Uri.EscapeDataString(businessAccountId)}/subscribed_apps", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        _ = await ReadResponseAsync<SuccessResponse>(response, cancellationToken);
    }

    public async Task RequestCoexistenceSyncAsync(string phoneNumberId, string accessToken, string syncType, CancellationToken cancellationToken = default)
    {
        if (!SupportedSyncTypes.Contains(syncType))
            throw new ArgumentOutOfRangeException(nameof(syncType));

        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"{Version}/{Uri.EscapeDataString(phoneNumberId)}/smb_app_data", accessToken);
        request.Content = JsonContent.Create(new { messaging_product = "whatsapp", sync_type = syncType });
        using var response = await SendAsync(request, cancellationToken);
        _ = await ReadResponseAsync<SyncRequestResponse>(response, cancellationToken);
    }

    private string Version =>
        !string.IsNullOrWhiteSpace(options.Value.GraphApiVersion) && options.Value.GraphApiVersion.All(c => char.IsAsciiLetterOrDigit(c) || c == '.')
            ? options.Value.GraphApiVersion
            : throw new InvalidOperationException("A versão Graph API do WhatsApp não está configurada.");

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                throw new InvalidOperationException("A Meta recusou uma operação de conexão WhatsApp.");
            }
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Não foi possível completar uma operação de conexão WhatsApp com a Meta.");
        }
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("A resposta da Meta para WhatsApp estava vazia.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("A resposta da Meta para WhatsApp não pôde ser validada.");
        }
    }

    private sealed record TokenExchangeResponse([property: JsonPropertyName("access_token")] string? AccessToken);
    private sealed record PhoneNumbersResponse([property: JsonPropertyName("data")] List<PhoneNumberItem>? Data);
    private sealed record PhoneNumberItem([property: JsonPropertyName("id")] string? Id, [property: JsonPropertyName("display_phone_number")] string? DisplayPhoneNumber);
    private sealed record SuccessResponse([property: JsonPropertyName("success")] bool Success);
    private sealed record SyncRequestResponse([property: JsonPropertyName("request_id")] string? RequestId);
}
