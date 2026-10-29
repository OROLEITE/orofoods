using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Orofoods.Web.Services.Integrations;

public sealed record WmcAuditTimestampDisplay(string Utc, string Brasilia);

public static class WmcAuditDisplayFormatter
{
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

    private static readonly Regex AuthorizationCredentialsRegex = new(
        @"(?<prefix>\b(?:authorization|proxy-authorization)\s*[:=]\s*)(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]+",
        SafeOptions);

    private static readonly Regex SchemeCredentialsRegex = new(
        @"(?<scheme>\b(?:Bearer|Basic)\s+)[A-Za-z0-9._~+/=-]+",
        SafeOptions);

    private static readonly Regex SensitiveAssignmentRegex = new(
        @"(?<prefix>[""']?(?:" + SensitiveKeyPattern + @")[""']?\s*(?:=|:)\s*)(?:""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|[^,\s;&}\]]+)",
        SafeOptions);

    private static readonly Regex SensitiveXmlElementRegex = new(
        @"(?<opening><(?<tag>(?:" + SensitiveKeyPattern + @"))\b[^>]*>).*?(?<closing></\k<tag>\s*>)",
        SafeOptions | RegexOptions.Singleline);

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

            RedactJsonNode(root);
            return root.ToJsonString();
        }
        catch (JsonException)
        {
            return value;
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
                else if (property.Value is not null)
                {
                    RedactJsonNode(property.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item is not null)
                {
                    RedactJsonNode(item);
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
        sanitized = AuthorizationCredentialsRegex.Replace(sanitized, "${prefix}[REDACTED]");
        sanitized = SchemeCredentialsRegex.Replace(sanitized, "${scheme}[REDACTED]");
        sanitized = SensitiveAssignmentRegex.Replace(sanitized, "${prefix}\"[REDACTED]\"");
        sanitized = SensitiveXmlElementRegex.Replace(sanitized, "${opening}[REDACTED]${closing}");
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
