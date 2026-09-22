using System.Text.RegularExpressions;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orofoods.Web.Infrastructure.Diagnostics;

internal static partial class AzureIdentityDiagnostics
{
    private static readonly Lazy<ILoggerFactory> CredentialProbeLoggerFactory = new(
        () => LoggerFactory.Create(logging => logging.AddConsole()));

    internal static bool IsCredentialProbeEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsStaging() && configuration.GetValue<bool>("Diagnostics:CredentialProbe");

    internal static TokenCredential WrapDataProtectionCredential(
        TokenCredential credential,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (!IsCredentialProbeEnabled(environment, configuration))
        {
            return credential;
        }

        return new DiagnosticTokenCredential(
            credential,
            CredentialProbeLoggerFactory.Value.CreateLogger<DiagnosticTokenCredential>());
    }

    internal static string Redact(string message)
    {
        var redacted = BearerTokenRegex().Replace(message, "$1[REDACTED]");
        redacted = SensitiveAssignmentRegex().Replace(redacted, "$1[REDACTED]");
        return JwtRegex().Replace(redacted, "[REDACTED]");
    }

    internal static TokenIdentityClaims ReadTokenIdentityClaims(string token)
    {
        var segments = token.Split('.');
        if (segments.Length < 2)
        {
            return TokenIdentityClaims.Empty;
        }

        try
        {
            var payload = segments[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = document.RootElement;
            return new TokenIdentityClaims(
                Claim(root, "oid"),
                Claim(root, "tid"),
                Claim(root, "xms_mirid"),
                Claim(root, "appid"));
        }
        catch (FormatException)
        {
            return TokenIdentityClaims.Empty;
        }
        catch (JsonException)
        {
            return TokenIdentityClaims.Empty;
        }
    }

    private static string? Claim(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex(@"(?i)(authorization|access[_-]?token|refresh[_-]?token|client[_-]?secret|identity[_-]?header|msi[_-]?secret|password|sig|sharedaccesssignature)\s*([:=])\s*([^\s,;&]+)")]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(@"(?i)(authorization\s*[:=]\s*bearer\s+)[^\s,;&]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+")]
    private static partial Regex JwtRegex();
}

internal sealed class DiagnosticTokenCredential(TokenCredential inner, ILogger<DiagnosticTokenCredential> logger) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenCore(requestContext, cancellationToken, "GetToken");
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        LogRequest("GetTokenAsync", requestContext);
        try
        {
            var accessToken = await inner.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
            LogSuccess("GetTokenAsync", requestContext, accessToken);
            return accessToken;
        }
        catch (Exception exception)
        {
            LogFailure("GetTokenAsync", exception);
            throw;
        }
    }

    private AccessToken GetTokenCore(TokenRequestContext requestContext, CancellationToken cancellationToken, string operation)
    {
        LogRequest(operation, requestContext);
        try
        {
            var accessToken = inner.GetToken(requestContext, cancellationToken);
            LogSuccess(operation, requestContext, accessToken);
            return accessToken;
        }
        catch (Exception exception)
        {
            LogFailure(operation, exception);
            throw;
        }
    }

    private void LogRequest(string operation, TokenRequestContext requestContext)
    {
        logger.LogInformation("Azure credential probe {Operation} scopes {Scopes}", operation, string.Join(' ', requestContext.Scopes));
    }

    private void LogSuccess(string operation, TokenRequestContext requestContext, AccessToken accessToken)
    {
        var claims = AzureIdentityDiagnostics.ReadTokenIdentityClaims(accessToken.Token);
        logger.LogInformation(
            "Azure credential probe {Operation} succeeded for scopes {Scopes}; oid {ObjectId}; tid {TenantId}; xms_mirid {ManagedIdentityResourceId}; appid {ApplicationId}",
            operation,
            string.Join(' ', requestContext.Scopes),
            claims.ObjectId,
            claims.TenantId,
            claims.ManagedIdentityResourceId,
            claims.ApplicationId);
    }

    private void LogFailure(string operation, Exception exception)
    {
        logger.LogWarning(
            "Azure credential probe {Operation} failed with {ExceptionType}: {Message}",
            operation,
            exception.GetType().Name,
            AzureIdentityDiagnostics.Redact(exception.Message));
    }
}

internal sealed record TokenIdentityClaims(
    string? ObjectId,
    string? TenantId,
    string? ManagedIdentityResourceId,
    string? ApplicationId)
{
    internal static TokenIdentityClaims Empty { get; } = new(null, null, null, null);
}