using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Commercial;

public sealed class MetaWhatsAppBusinessGateway(HttpClient httpClient, IOptions<WhatsAppBusinessOptions> options) : IWhatsAppBusinessGateway
{
    public async Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled) return new(false, null, "DISABLED", "WhatsApp Business está desabilitado.");
        if (string.IsNullOrWhiteSpace(settings.GraphApiVersion) || string.IsNullOrWhiteSpace(settings.PhoneNumberId) || string.IsNullOrWhiteSpace(settings.AccessToken))
            return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.GraphApiVersion}/{settings.PhoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        request.Content = JsonContent.Create(new { messaging_product = "whatsapp", recipient_type = "individual", to = phoneNumber, type = "text", text = new { preview_url = false, body = text } });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return new(false, null, ((int)response.StatusCode).ToString(), "Falha ao enviar mensagem WhatsApp.");
        var payload = await response.Content.ReadFromJsonAsync<MetaSendResponse>(cancellationToken: cancellationToken);
        return payload?.Messages?.FirstOrDefault()?.Id is { Length: > 0 } id ? new(true, id, null, null) : new(false, null, "INVALID_RESPONSE", "Resposta inválida da Meta.");
    }

    private sealed record MetaSendResponse(List<MetaMessage>? Messages);
    private sealed record MetaMessage(string? Id);
}
