using System.Globalization;
using System.Net;
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
    ILogger<MetaWhatsAppBusinessGateway> logger,
    IWhatsAppAccessTokenProvider? accessTokenProvider = null,
    IWhatsAppPhoneNumberIdProvider? phoneNumberIdProvider = null,
    IWhatsAppBusinessAccountIdProvider? businessAccountIdProvider = null) : IWhatsAppBusinessGateway
{
    private static readonly EventId MetaSendRejectedEvent = new(4201, "WhatsApp.MetaSendRejected");
    private static readonly JsonSerializerOptions MetaErrorJsonOptions = new() { MaxDepth = 16 };

    public async Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled) return new(false, null, "DISABLED", "WhatsApp Business está desabilitado.");
        if (string.IsNullOrWhiteSpace(settings.GraphApiVersion) ||
            (phoneNumberIdProvider is null && string.IsNullOrWhiteSpace(settings.PhoneNumberId)) ||
            (accessTokenProvider is null && string.IsNullOrWhiteSpace(settings.AccessToken)))
            return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado.");
        string accessToken;
        try { accessToken = accessTokenProvider is null ? settings.AccessToken : await accessTokenProvider.GetAsync(cancellationToken); }
        catch (InvalidOperationException) { return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado."); }
        if (string.IsNullOrWhiteSpace(accessToken)) return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado.");
        string phoneNumberId;
        try { phoneNumberId = phoneNumberIdProvider is null ? settings.PhoneNumberId : await phoneNumberIdProvider.GetAsync(cancellationToken); }
        catch (InvalidOperationException) { return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado."); }
        if (string.IsNullOrWhiteSpace(phoneNumberId)) return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.GraphApiVersion}/{phoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
                MaskIdentifier(phoneNumberId),
                MaskRecipient(phoneNumber));

            return new(false, null, SendFailureCode(response.StatusCode), "Falha ao enviar mensagem WhatsApp.");
        }
        var payload = await response.Content.ReadFromJsonAsync<MetaSendResponse>(cancellationToken: cancellationToken);
        return payload?.Messages?.FirstOrDefault()?.Id is { Length: > 0 } id ? new(true, id, null, null) : new(false, null, "INVALID_RESPONSE", "Resposta inválida da Meta.");
    }

    public async Task<IReadOnlyList<WhatsAppApprovedTemplate>> GetApprovedTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return [];

        var credentials = await GetTemplateCredentialsAsync(settings, requirePhoneNumberId: false, cancellationToken);
        var templates = new List<WhatsAppApprovedTemplate>();
        string? after = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 0; page < 100; page++)
        {
            var query = $"{settings.GraphApiVersion}/{Uri.EscapeDataString(credentials.BusinessAccountId)}/message_templates?fields=name,status,category,language,components&limit=100";
            if (!string.IsNullOrWhiteSpace(after))
                query += "&after=" + Uri.EscapeDataString(after);

            using var request = new HttpRequestMessage(HttpMethod.Get, query);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Não foi possível consultar os modelos aprovados do WhatsApp.");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("data", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var template = ParseApprovedTemplate(item);
                    if (template is not null)
                        templates.Add(template);
                }
            }

            after = ReadAfterCursor(root);
            if (after is null || !seenCursors.Add(after))
                break;
        }

        return templates
            .GroupBy(template => $"{template.Name}\u001f{template.Language}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(template => template.Name, StringComparer.Ordinal)
            .ThenBy(template => template.Language, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> headerParameters,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? bodyParameterNames = null,
        IReadOnlyList<string>? headerParameterNames = null)
    {
        var settings = options.Value;
        if (!settings.Enabled)
            return new(false, null, "DISABLED", "WhatsApp Business está desabilitado.");
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(templateName) || string.IsNullOrWhiteSpace(language) ||
            bodyParameters.Any(string.IsNullOrWhiteSpace) || headerParameters.Any(string.IsNullOrWhiteSpace) ||
            !AreValidTemplateParameters(bodyParameters, bodyParameterNames) || !AreValidTemplateParameters(headerParameters, headerParameterNames))
            return new(false, null, "INVALID_REQUEST", "Não foi possível validar o modelo WhatsApp.");

        (string AccessToken, string BusinessAccountId, string PhoneNumberId) credentials;
        try
        {
            credentials = await GetTemplateCredentialsAsync(settings, requirePhoneNumberId: true, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new(false, null, "NOT_CONFIGURED", "WhatsApp Business não está configurado.");
        }

        var components = new List<object>();
        if (headerParameters.Count > 0)
            components.Add(new { type = "header", parameters = CreateTemplateParameters(headerParameters, headerParameterNames) });
        if (bodyParameters.Count > 0)
            components.Add(new { type = "body", parameters = CreateTemplateParameters(bodyParameters, bodyParameterNames) });

        var template = new Dictionary<string, object?>
        {
            ["name"] = templateName,
            ["language"] = new { code = language }
        };
        if (components.Count > 0)
            template["components"] = components;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.GraphApiVersion}/{Uri.EscapeDataString(credentials.PhoneNumberId)}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phoneNumber,
            type = "template",
            template
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var metaError = await ReadMetaErrorAsync(response.Content, cancellationToken);
            logger.LogWarning(
                MetaSendRejectedEvent,
                "Meta rejected WhatsApp template send. HttpStatus={HttpStatus} MetaErrorCode={MetaErrorCode} PhoneNumberId={PhoneNumberId} Recipient={Recipient}",
                (int)response.StatusCode,
                ReadNumericErrorValue(metaError?.Code),
                MaskIdentifier(credentials.PhoneNumberId),
                MaskRecipient(phoneNumber));
            return new(false, null, SendFailureCode(response.StatusCode), "Falha ao enviar modelo WhatsApp.");
        }

        var payload = await response.Content.ReadFromJsonAsync<MetaSendResponse>(cancellationToken: cancellationToken);
        return payload?.Messages?.FirstOrDefault()?.Id is { Length: > 0 } id
            ? new(true, id, null, null)
            : new(false, null, "INVALID_RESPONSE", "Resposta inválida da Meta.");
    }

    private async Task<(string AccessToken, string BusinessAccountId, string PhoneNumberId)> GetTemplateCredentialsAsync(
        WhatsAppBusinessOptions settings,
        bool requirePhoneNumberId,
        CancellationToken cancellationToken)
    {
        if (!IsGraphApiVersionValid(settings.GraphApiVersion))
            throw new InvalidOperationException("WhatsApp Business não está configurado.");

        if (settings.EmbeddedSignupEnabled && (accessTokenProvider is null || businessAccountIdProvider is null || (requirePhoneNumberId && phoneNumberIdProvider is null)))
            throw new InvalidOperationException("WhatsApp Business não está configurado.");

        var accessToken = accessTokenProvider is null ? settings.AccessToken : await accessTokenProvider.GetAsync(cancellationToken);
        var businessAccountId = businessAccountIdProvider is null ? settings.BusinessAccountId : await businessAccountIdProvider.GetAsync(cancellationToken);
        var phoneNumberId = requirePhoneNumberId
            ? phoneNumberIdProvider is null ? settings.PhoneNumberId : await phoneNumberIdProvider.GetAsync(cancellationToken)
            : string.Empty;

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(businessAccountId) || (requirePhoneNumberId && string.IsNullOrWhiteSpace(phoneNumberId)))
            throw new InvalidOperationException("WhatsApp Business não está configurado.");

        return (accessToken, businessAccountId, phoneNumberId);
    }

    private static bool IsGraphApiVersionValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '.');

    private static string SendFailureCode(HttpStatusCode statusCode) =>
        (int)statusCode >= 500 ? "UNKNOWN" : ((int)statusCode).ToString(CultureInfo.InvariantCulture);

    private static string? ReadAfterCursor(JsonElement root)
    {
        if (!root.TryGetProperty("paging", out var paging) ||
            !paging.TryGetProperty("next", out var next) ||
            next.ValueKind != JsonValueKind.String ||
            !paging.TryGetProperty("cursors", out var cursors) ||
            !cursors.TryGetProperty("after", out var after) ||
            after.ValueKind != JsonValueKind.String)
            return null;

        return after.GetString();
    }

    private static WhatsAppApprovedTemplate? ParseApprovedTemplate(JsonElement item)
    {
        if (!TryReadString(item, "status", out var status) || !string.Equals(status, "APPROVED", StringComparison.Ordinal) ||
            !TryReadString(item, "name", out var name) || !TryReadString(item, "language", out var language) ||
            !TryReadString(item, "category", out var category) ||
            !item.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
            return null;

        string? headerText = null;
        string? bodyText = null;
        foreach (var component in components.EnumerateArray())
        {
            if (!TryReadString(component, "type", out var type))
                continue;

            if (string.Equals(type, "HEADER", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(component, "format", out var format) || !string.Equals(format, "TEXT", StringComparison.OrdinalIgnoreCase) ||
                    !TryReadString(component, "text", out headerText))
                    return null;
            }
            else if (string.Equals(type, "BODY", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(component, "text", out bodyText))
                    return null;
            }
            else if (string.Equals(type, "BUTTONS", StringComparison.OrdinalIgnoreCase) &&
                     component.TryGetProperty("buttons", out var buttons) && buttons.ValueKind == JsonValueKind.Array &&
                     buttons.EnumerateArray().Any(button => TryReadString(button, "url", out var url) && CountParameters(url) > 0))
            {
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(bodyText) || !TryDescribeTemplateParameters(
                headerText,
                bodyText,
                out var headerParameterCount,
                out var bodyParameterCount,
                out var headerParameterNames,
                out var bodyParameterNames))
            return null;

        return new WhatsAppApprovedTemplate(
            name,
            language,
            category,
            headerText,
            bodyText,
            headerParameterCount,
            bodyParameterCount,
            headerParameterNames,
            bodyParameterNames);
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString()!);
    }

    private static int CountParameters(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : Regex.Matches(value, @"\{\{\s*(?:\d+|[A-Za-z_][A-Za-z0-9_]*)\s*\}\}").Count;

    private static bool TryDescribeTemplateParameters(
        string? headerText,
        string bodyText,
        out int headerCount,
        out int bodyCount,
        out IReadOnlyList<string>? headerNames,
        out IReadOnlyList<string>? bodyNames)
    {
        var headerTokens = ReadTemplateParameterTokens(headerText);
        var bodyTokens = ReadTemplateParameterTokens(bodyText);
        var allTokens = headerTokens.Concat(bodyTokens).ToArray();
        var hasNamedParameters = allTokens.Any(token => !int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _));
        var hasPositionalParameters = allTokens.Any(token => int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _));
        if (hasNamedParameters && hasPositionalParameters)
        {
            headerCount = bodyCount = 0;
            headerNames = bodyNames = null;
            return false;
        }

        if (hasNamedParameters)
        {
            headerNames = headerTokens.Distinct(StringComparer.Ordinal).ToArray();
            bodyNames = bodyTokens.Distinct(StringComparer.Ordinal).ToArray();
            headerCount = headerNames.Count;
            bodyCount = bodyNames.Count;
        }
        else
        {
            headerNames = bodyNames = null;
            headerCount = headerTokens.Length;
            bodyCount = bodyTokens.Length;
        }

        return true;
    }

    private static string[] ReadTemplateParameterTokens(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : Regex.Matches(value, @"\{\{\s*(\d+|[A-Za-z_][A-Za-z0-9_]*)\s*\}\}")
                .Select(match => match.Groups[1].Value)
                .ToArray();

    private static bool AreValidTemplateParameters(IReadOnlyList<string> values, IReadOnlyList<string>? names) =>
        names is null || names.Count == values.Count && names.All(name => !string.IsNullOrWhiteSpace(name));

    private static object[] CreateTemplateParameters(IReadOnlyList<string> values, IReadOnlyList<string>? names) =>
        values.Select((value, index) =>
        {
            var parameter = new Dictionary<string, object?> { ["type"] = "text", ["text"] = value };
            if (names is not null)
                parameter["parameter_name"] = names[index];
            return (object)parameter;
        }).ToArray();

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
