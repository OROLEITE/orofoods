using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Orofoods.Web.Services.Integrations;

public sealed record WmcAuditTimestampDisplay(string Utc, string Brasilia);

public static class WmcAuditDisplayFormatter
{
    private const int MaximumMarkupDecodePasses = 8;
    private const RegexOptions SafeOptions = RegexOptions.IgnoreCase
        | RegexOptions.CultureInvariant
        | RegexOptions.Compiled;

    private const string SensitiveKeyPattern =
        "password|passwd|pwd|senha|secret|credential|credencial|client[_-]?secret|token|access[_-]?token|refresh[_-]?token|api[_-]?key|authorization|serial|imei|cpf|cnpj|e-?mail|email|phone|telefone|celular|mobile|whatsapp|address|endereco|nome|name|codcliente|idcliente|customer|client|user(?:name|[_-]?id)?|login|usuario";

    private static readonly string[] SensitiveJsonKeyFragments =
    [
        "password", "passwd", "pwd", "senha", "secret", "credential", "credencial",
        "token", "apikey", "authorization", "serial", "imei", "cpf", "cnpj", "email",
        "phone", "telefone", "celular", "mobile", "whatsapp", "address", "endereco",
        "nome", "name", "codcliente", "idcliente", "customer", "client", "user", "login", "usuario"
    ];

    private static readonly TimeZoneInfo BrasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static readonly Regex AuthorizationHeaderRegex = new(
        @"(?<prefix>\b(?:authorization|proxy-authorization)\s*[:=]\s*)[^\r\n]*",
        SafeOptions);

    private static readonly Regex SchemeCredentialsRegex = new(
        @"(?<scheme>\b(?:Bearer|Basic)\s+)[A-Za-z0-9._~+/=-]+",
        SafeOptions);

    private static readonly Regex SensitiveAssignmentRegex = new(
        @"(?<prefix>[""']?(?:" + SensitiveKeyPattern + @")[""']?\s*(?:=|:)\s*)(?:""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|[^,\r\n;&}\]>]+)",
        SafeOptions);

    private static readonly Regex XmlTagStartRegex = new(
        @"<(?<tag>[A-Za-z_][A-Za-z0-9_.:-]*)",
        SafeOptions);

    private static readonly Regex UrlCredentialsRegex = new(
        @"(?<scheme>https?://)[^\s/@:]+:[^\s/@]+@",
        SafeOptions);

    private static readonly Regex EmailRegex = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        SafeOptions);

    private static readonly Regex CnpjRegex = new(
        @"(?<!\d)\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}(?!\d)",
        SafeOptions);

    private static readonly Regex CpfRegex = new(
        @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)",
        SafeOptions);

    private static readonly Regex BrazilianPhoneRegex = new(
        @"(?<!\d)(?:\+?55[\s().-]*)?\(?\d{2}\)?[\s.-]+9?\d{4}[\s.-]?\d{4}(?!\d)",
        SafeOptions);

    private static string SanitizeJsonFields(string value)
    {
        try
        {
            var root = JsonNode.Parse(value);
            if (root is null)
            {
                return value;
            }

            if (root is JsonValue rootValue && rootValue.TryGetValue<string>(out var rootString))
            {
                return JsonSerializer.Serialize(SanitizeXmlMarkupString(rootString));
            }

            RedactJsonNode(root);
            return root.ToJsonString();
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string SanitizeXmlFields(string value)
    {
        if (value.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            try
            {
                var document = XDocument.Parse(value, LoadOptions.PreserveWhitespace);
                if (document.Root is not null)
                {
                    RedactXmlElement(document.Root);
                    return document.ToString(SaveOptions.DisableFormatting);
                }
            }
            catch (System.Xml.XmlException)
            {
                // Audit bodies can be truncated at the storage limit. Preserve surrounding
                // diagnostics while failing closed from any sensitive XML tag to its close
                // or the end of the body.
            }
        }

        // Error responses can mix prose and XML fragments, and JSON messages can contain
        // XML snippets. Scan these bodies too, rather than only parsing a document root.
        return RedactXmlFragments(value);
    }

    private static string RedactXmlFragments(string value)
    {
        var matches = XmlTagStartRegex.Matches(value);
        var output = new StringBuilder(value.Length);
        var copiedThrough = 0;

        foreach (Match match in matches)
        {
            if (match.Index < copiedThrough)
            {
                continue;
            }

            var qualifiedName = match.Groups["tag"].Value;
            var localName = qualifiedName[(qualifiedName.LastIndexOf(':') + 1)..];
            if (!IsSensitiveJsonKey(localName))
            {
                continue;
            }

            var startTagEnd = FindXmlTagEnd(value, match.Index + match.Length);
            var startTagIsComplete = startTagEnd >= 0;
            var isSelfClosing = startTagIsComplete && IsSelfClosingXmlTag(value, match.Index, startTagEnd);
            var redactionEnd = value.Length;

            if (startTagIsComplete && isSelfClosing)
            {
                redactionEnd = startTagEnd + 1;
            }

            output.Append(value, copiedThrough, match.Index - copiedThrough);
            output.Append("[REDACTED]");
            copiedThrough = redactionEnd;
        }

        if (copiedThrough == 0)
        {
            return value;
        }

        output.Append(value, copiedThrough, value.Length - copiedThrough);
        return output.ToString();
    }

    private static string SanitizeXmlMarkupString(string value)
    {
        var candidate = value;
        var sanitizerChangedContent = false;
        for (var pass = 0; pass < MaximumMarkupDecodePasses; pass++)
        {
            var sanitized = SanitizeXmlFields(candidate);
            if (!string.Equals(sanitized, candidate, StringComparison.Ordinal))
            {
                candidate = sanitized;
                sanitizerChangedContent = true;
            }

            var decoded = WebUtility.HtmlDecode(candidate);
            if (string.Equals(decoded, candidate, StringComparison.Ordinal))
            {
                return sanitizerChangedContent ? candidate : value;
            }

            candidate = decoded;
        }

        return "[REDACTED]";
    }

    private static int FindXmlTagEnd(string value, int searchStart)
    {
        var quote = '\0';
        for (var index = searchStart; index < value.Length; index++)
        {
            var character = value[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsSelfClosingXmlTag(string value, int start, int tagEnd)
    {
        var lastContentCharacter = tagEnd - 1;
        while (lastContentCharacter >= start && char.IsWhiteSpace(value[lastContentCharacter]))
        {
            lastContentCharacter--;
        }

        return lastContentCharacter >= start && value[lastContentCharacter] == '/';
    }

    private static void RedactXmlElement(XElement element)
    {
        var sensitiveElement = IsSensitiveJsonKey(element.Name.LocalName);
        foreach (var attribute in element.Attributes())
        {
            if (sensitiveElement || IsSensitiveJsonKey(attribute.Name.LocalName))
            {
                attribute.Value = "[REDACTED]";
            }
            else if (attribute.Value.Contains('<', StringComparison.Ordinal))
            {
                attribute.Value = SanitizeXmlMarkupString(attribute.Value);
            }
        }

        if (sensitiveElement)
        {
            element.ReplaceNodes(new XText("[REDACTED]"));
            return;
        }

        foreach (var node in element.Nodes().ToList())
        {
            if (node is XElement child)
            {
                RedactXmlElement(child);
            }
            else if (node is XText text && text.Value.Contains('<', StringComparison.Ordinal))
            {
                text.Value = SanitizeXmlMarkupString(text.Value);
            }
            else if (node is XComment comment)
            {
                comment.Value = SanitizeXmlMarkupString(comment.Value);
            }
            else if (node is XProcessingInstruction instruction)
            {
                instruction.Data = SanitizeXmlMarkupString(instruction.Data);
            }
        }
    }

    private static void RedactJsonNode(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                if (IsSensitiveJsonKey(property.Key))
                {
                    jsonObject[property.Key] = "[REDACTED]";
                }
                else if (property.Value is JsonValue jsonValue
                    && jsonValue.TryGetValue<string>(out var stringValue))
                {
                    jsonObject[property.Key] = SanitizeXmlMarkupString(stringValue);
                }
                else if (property.Value is not null)
                {
                    RedactJsonNode(property.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                var item = jsonArray[index];
                if (item is not null)
                {
                    if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
                    {
                        jsonArray[index] = SanitizeXmlMarkupString(stringValue);
                    }
                    else
                    {
                        RedactJsonNode(item);
                    }
                }
            }
        }
    }

    private static bool IsSensitiveJsonKey(string key)
    {
        var normalized = Regex.Replace(key, "[^A-Za-z0-9]", string.Empty);
        return SensitiveJsonKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = SanitizeJsonFields(value);
        sanitized = SanitizeXmlMarkupString(sanitized);
        sanitized = AuthorizationHeaderRegex.Replace(sanitized, "${prefix}[REDACTED]");
        sanitized = SchemeCredentialsRegex.Replace(sanitized, "${scheme}[REDACTED]");
        sanitized = SensitiveAssignmentRegex.Replace(sanitized, "${prefix}\"[REDACTED]\"");
        sanitized = UrlCredentialsRegex.Replace(sanitized, "${scheme}[REDACTED]@");
        sanitized = EmailRegex.Replace(sanitized, "[REDACTED]");
        sanitized = CnpjRegex.Replace(sanitized, "[REDACTED]");
        sanitized = CpfRegex.Replace(sanitized, "[REDACTED]");
        sanitized = BrazilianPhoneRegex.Replace(sanitized, "[REDACTED]");
        return sanitized;
    }

    public static WmcAuditTimestampDisplay FormatTimestamp(DateTime timestamp)
    {
        // PostgreSQL timestamptz values are read as UTC. Specify the kind explicitly so
        // presentation never depends on the App Service host's local timezone.
        var utc = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
        var brasilia = TimeZoneInfo.ConvertTime(utc, BrasiliaTimeZone);

        return new WmcAuditTimestampDisplay(
            utc.ToString("yyyy-MM-dd HH:mm:ss zzz 'UTC'", CultureInfo.InvariantCulture),
            $"{brasilia.ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture)} (America/Sao_Paulo)");
    }
}
