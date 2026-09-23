using System.Text.Json.Serialization;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AppDataProtectionOptions = Orofoods.Web.Models.Configuration.DataProtectionOptions;

namespace Orofoods.Web.Infrastructure.Diagnostics;

internal static class MainDataProtectionBlobReadEndpoint
{
    internal const string Route = "/_internal/diagnostics/dataprotection-main-blob-read";

    internal static bool IsAvailable(IHostEnvironment environment, IConfiguration configuration) =>
        IsolatedDataProtectionProbeEndpoint.IsAvailable(environment, configuration);

    internal static void MapIfAvailable(WebApplication app)
    {
        if (IsAvailable(app.Environment, app.Configuration))
        {
            Map(app);
        }
    }

    internal static void Map(WebApplication app)
    {
        app.MapPost(Route, async (
            HttpContext context,
            IsolatedDataProtectionProbe authorizationProbe,
            IMainDataProtectionBlobReadDiagnostic diagnostic,
            CancellationToken cancellationToken) =>
            await HandleAsync(context, authorizationProbe, diagnostic, cancellationToken));
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext context,
        IsolatedDataProtectionProbe authorizationProbe,
        IMainDataProtectionBlobReadDiagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (!authorizationProbe.IsAuthorized(context.Request.Headers["X-Orofoods-Diagnostic-Key"].ToString()))
        {
            return Results.NotFound();
        }

        authorizationProbe.LogHandlerEntered();
        var result = await diagnostic.ExecuteAsync(cancellationToken);
        return Results.Json(
            result,
            statusCode: result.DownloadSucceeded
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
    }
}

internal interface IMainDataProtectionBlobReadDiagnostic
{
    Task<MainDataProtectionBlobReadResult> ExecuteAsync(CancellationToken cancellationToken);
}

internal sealed class MainDataProtectionBlobReadDiagnostic(
    IConfiguration configuration,
    IHostEnvironment environment,
    IMainDataProtectionBlobReadClientFactory clientFactory,
    ILoggerFactory loggerFactory) : IMainDataProtectionBlobReadDiagnostic
{
    public async Task<MainDataProtectionBlobReadResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!MainDataProtectionBlobReadEndpoint.IsAvailable(environment, configuration))
        {
            return MainDataProtectionBlobReadResult.Failure(false, StatusCodes.Status503ServiceUnavailable, "Unavailable");
        }

        var settings = configuration.GetSection("DataProtection").Get<AppDataProtectionOptions>()?.Azure;
        if (settings is not { Enabled: true } ||
            string.IsNullOrWhiteSpace(settings.BlobUri) ||
            !Uri.TryCreate(settings.BlobUri, UriKind.Absolute, out var blobUri) ||
            !string.Equals(blobUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(blobUri.Segments.LastOrDefault()?.Trim('/'), "keys.xml", StringComparison.Ordinal))
        {
            return MainDataProtectionBlobReadResult.Failure(false, StatusCodes.Status503ServiceUnavailable, "InvalidConfiguration");
        }

        var credential = new DiagnosticTokenCredential(
            new DefaultAzureCredential(),
            loggerFactory.CreateLogger<DiagnosticTokenCredential>());
        var client = clientFactory.Create(blobUri, credential);
        var propertiesSucceeded = false;

        try
        {
            _ = await client.GetPropertiesAsync(cancellationToken);
            propertiesSucceeded = true;
            var httpStatus = await client.DownloadToNullAsync(cancellationToken);
            return MainDataProtectionBlobReadResult.Success(httpStatus);
        }
        catch (RequestFailedException exception)
        {
            return MainDataProtectionBlobReadResult.Failure(
                propertiesSucceeded,
                exception.Status > 0 ? exception.Status : StatusCodes.Status503ServiceUnavailable,
                SanitizeErrorCode(exception.ErrorCode));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MainDataProtectionBlobReadResult.Failure(
                propertiesSucceeded,
                StatusCodes.Status500InternalServerError,
                exception.GetType().Name);
        }
    }

    private static string? SanitizeErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return null;
        }

        return errorCode.Length <= 128 && errorCode.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            ? errorCode
            : "Redacted";
    }
}

internal interface IMainDataProtectionBlobReadClientFactory
{
    IMainDataProtectionBlobReadClient Create(Uri blobUri, TokenCredential credential);
}

internal sealed class MainDataProtectionBlobReadClientFactory : IMainDataProtectionBlobReadClientFactory
{
    public IMainDataProtectionBlobReadClient Create(Uri blobUri, TokenCredential credential) =>
        new MainDataProtectionBlobReadClient(new BlobClient(blobUri, credential));
}

internal interface IMainDataProtectionBlobReadClient
{
    Task<int> GetPropertiesAsync(CancellationToken cancellationToken);
    Task<int> DownloadToNullAsync(CancellationToken cancellationToken);
}

internal sealed class MainDataProtectionBlobReadClient(BlobClient client) : IMainDataProtectionBlobReadClient
{
    public async Task<int> GetPropertiesAsync(CancellationToken cancellationToken)
    {
        var response = await client.GetPropertiesAsync(cancellationToken: cancellationToken);
        return response.GetRawResponse().Status;
    }

    public async Task<int> DownloadToNullAsync(CancellationToken cancellationToken)
    {
        var response = await client.DownloadStreamingAsync(cancellationToken: cancellationToken);
        await using var content = response.Value.Content;
        await content.CopyToAsync(Stream.Null, cancellationToken);
        return response.GetRawResponse().Status;
    }
}

internal sealed record MainDataProtectionBlobReadResult(
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("propertiesSucceeded")] bool PropertiesSucceeded,
    [property: JsonPropertyName("downloadSucceeded")] bool DownloadSucceeded,
    [property: JsonPropertyName("httpStatus")] int HttpStatus,
    [property: JsonPropertyName("errorCode")] string? ErrorCode)
{
    internal static MainDataProtectionBlobReadResult Success(int httpStatus) =>
        new("keys.xml", true, true, httpStatus, null);

    internal static MainDataProtectionBlobReadResult Failure(
        bool propertiesSucceeded,
        int httpStatus,
        string? errorCode) =>
        new("keys.xml", propertiesSucceeded, false, httpStatus, errorCode);
}
