using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Commercial;

public sealed class MetaWhatsAppBusinessGateway(
    HttpClient httpClient,
    IOptions<WhatsAppBusinessOptions> options,
    ILogger<MetaWhatsAppBusinessGateway> logger) : IWhatsAppBusinessGateway
{
    private static readonly EventId MetaSendRejectedEvent = new(4201, "WhatsApp.MetaSendRejected");
    private static readonly JsonSerializerOptions MetaErrorJsonOptions = new() { MaxDepth = 16 };

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
        if (!response.IsSuccessStatusCode)
        {
            var metaError = await ReadMetaErrorAsync(response.Content, cancellationToken);
            logger.LogWarning(
                MetaSendRejectedEvent,
                "Meta rejected WhatsApp send. HttpStatus={HttpStatus} MetaErrorCode={MetaErrorCode} MetaErrorSubcode={MetaErrorSubcode} MetaErrorType={MetaErrorType} MetaErrorMessage={MetaErrorMessage} FbtraceId={FbtraceId} PhoneNumberId={PhoneNumberId} Recipient={Recipient}",
                (int)response.StatusCode,
                ReadNumericErrorValue(metaError?.Code),
                ReadNumericErrorValue(metaError?.ErrorSubcode),
                SanitizeMetaType(metaError?.Type),
                SanitizeMetaMessage(metaError?.Message, settings, phoneNumber, text),
                SanitizeFbtraceId(metaError?.FbtraceId),
                MaskIdentifier(settings.PhoneNumberId),
                MaskRecipient(phoneNumber));

            return new(false, null, ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), "Falha ao enviar mensagem WhatsApp.");
        }
        var payload = await response.Content.ReadFromJsonAsync<MetaSendResponse>(cancellationToken: cancellationToken);
        return payload?.Messages?.FirstOrDefault()?.Id is { Length: > 0 } id ? new(true, id, null, null) : new(false, null, "INVALID_RESPONSE", "Resposta inválida da Meta.");
    }

    private static async Task<MetaSendError?> ReadMetaErrorAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null) return null;

        try
        {
            var responseBody = await content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<MetaSendErrorEnvelope>(responseBody, MetaErrorJsonOptions)?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadNumericErrorValue(JsonElement? value)
    {
        if (value is not { } element) return null;

        var rawValue = element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString(),
            _ => null
        };

        return long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    private static string? SanitizeMetaType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 64 && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-')
            ? normalized
            : "[REDACTED]";
    }

    private static string? SanitizeFbtraceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 128 && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')
            ? normalized
            : null;
    }

    private static string? SanitizeMetaMessage(string? message, WhatsAppBusinessOptions settings, string phoneNumber, string text)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var sanitized = message.Trim();
        foreach (var sensitiveValue in new[]
        {
            settings.AccessToken,
            settings.AppSecret,
            settings.VerifyToken,
            settings.PhoneNumberId,
            phoneNumber,
            new string(phoneNumber.Where(char.IsAsciiDigit).ToArray()),
            text
        }.Where(value => !string.IsNullOrEmpty(value)))
        {
            sanitized = sanitized.Replace(sensitiveValue, "[REDACTED]", StringComparison.OrdinalIgnoreCase);
        }

        sanitized = Regex.Replace(sanitized, @"(?i)\b(?:access[_\s-]?token|authorization|app[_\s-]?secret|verify[_\s-]?token)\s*[:=]\s*(?:bearer\s+)?[^\s,;]+", "[REDACTED_CREDENTIAL]");
        sanitized = Regex.Replace(sanitized, @"(?i)\bbearer\s+[^\s,;]+", "Bearer [REDACTED]");
        sanitized = Regex.Replace(sanitized, @"(?<!\d)\+?\d{8,15}(?!\d)", "[REDACTED_PHONE]");
        sanitized = Regex.Replace(sanitized, @"(?i)\b[\w.+-]+@[\w.-]+\.[a-z]{2,}\b", "[REDACTED_EMAIL]");
        sanitized = string.Concat(sanitized.Select(character => char.IsControl(character) ? ' ' : character));
        sanitized = Regex.Replace(sanitized, @"\s{2,}", " ").Trim();

        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    private static string MaskIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "[UNKNOWN]";
        var suffixLength = Math.Min(4, value.Length);
        return new string('*', Math.Max(4, value.Length - suffixLength)) + value[^suffixLength..];
    }

    private static string MaskRecipient(string? phoneNumber)
    {
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return "[UNKNOWN]";
        var suffixLength = Math.Min(4, digits.Length);
        return new string('*', Math.Max(4, digits.Length - suffixLength)) + digits[^suffixLength..];
    }

    private sealed record MetaSendErrorEnvelope([property: JsonPropertyName("error")] MetaSendError? Error);

    private sealed record MetaSendError(
        [property: JsonPropertyName("code")] JsonElement? Code,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("error_subcode")] JsonElement? ErrorSubcode,
        [property: JsonPropertyName("fbtrace_id")] string? FbtraceId);

    private sealed record MetaSendResponse(List<MetaMessage>? Messages);
    private sealed record MetaMessage(string? Id);
}
