using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Services.Payments;

public sealed class MercadoPagoWebhookSignatureValidator(
    IOptions<MercadoPagoOptions> options,
    IOptions<MercadoPagoPointOptions> pointOptions,
    TimeProvider timeProvider) : IMercadoPagoWebhookSignatureValidator
{
    private readonly string[] _webhookSecrets =
    [
        options.Value.WebhookSecret,
        pointOptions.Value.WebhookSecret
    ];

    public bool IsValid(string? signature, string? requestId, string? dataId)
    {
        if (string.IsNullOrWhiteSpace(signature)
            || string.IsNullOrWhiteSpace(requestId)
            || string.IsNullOrWhiteSpace(dataId))
        {
            return false;
        }

        var parts = signature.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var timestampText = GetPart(parts, "ts");
        var receivedHashText = GetPart(parts, "v1");
        if (!long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || string.IsNullOrWhiteSpace(receivedHashText))
        {
            return false;
        }

        byte[] receivedHash;
        try
        {
            receivedHash = Convert.FromHexString(receivedHashText);
        }
        catch (FormatException)
        {
            return false;
        }

        if (receivedHash.Length != 32)
        {
            return false;
        }

        var timestampMilliseconds = timestamp >= 100_000_000_000L
            ? timestamp
            : timestamp * 1000L;
        var drift = Math.Abs(timeProvider.GetUtcNow().ToUnixTimeMilliseconds() - timestampMilliseconds);
        if (drift > options.Value.WebhookSignatureTolerance.TotalMilliseconds)
        {
            return false;
        }

        var manifest = $"id:{dataId.Trim().ToLowerInvariant()};request-id:{requestId.Trim()};ts:{timestampText};";
        var manifestBytes = Encoding.UTF8.GetBytes(manifest);
        var valid = false;
        foreach (var secret in _webhookSecrets.Where(secret => !string.IsNullOrWhiteSpace(secret)).Distinct(StringComparer.Ordinal))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expectedHash = hmac.ComputeHash(manifestBytes);
            valid |= CryptographicOperations.FixedTimeEquals(expectedHash, receivedHash);
        }
        return valid;
    }

    private static string? GetPart(IEnumerable<string> parts, string key)
    {
        foreach (var part in parts)
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || separator == part.Length - 1)
            {
                continue;
            }

            if (string.Equals(part[..separator].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return part[(separator + 1)..].Trim();
            }
        }

        return null;
    }
}
