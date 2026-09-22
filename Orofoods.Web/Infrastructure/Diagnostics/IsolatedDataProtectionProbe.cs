using System.Security.Cryptography;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AppDataProtectionOptions = Orofoods.Web.Models.Configuration.DataProtectionOptions;

namespace Orofoods.Web.Infrastructure.Diagnostics;

internal static class IsolatedDataProtectionProbeEndpoint
{
    internal const string Route = "/_internal/diagnostics/dataprotection-probe";

    internal static bool IsAvailable(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsStaging() && configuration.GetValue<bool>("Diagnostics:CredentialProbe");

    internal static void Map(WebApplication app)
    {
        app.MapPost(Route, async (
            HttpContext context,
            IsolatedDataProtectionProbe probe,
            CancellationToken cancellationToken) =>
        {
            if (!probe.IsAuthorized(context.Request.Headers["X-Orofoods-Diagnostic-Key"].ToString()))
            {
                return Results.NotFound();
            }

            var result = await probe.ExecuteAsync(cancellationToken);
            return result.Success
                ? Results.Ok(new { success = true })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });
    }
}

internal sealed class IsolatedDataProtectionProbe(
    IConfiguration configuration,
    IHostEnvironment environment,
    IIsolatedDataProtectionProviderFactory providerFactory,
    ILoggerFactory loggerFactory,
    ILogger<IsolatedDataProtectionProbe> logger)
{
    internal const string ApplicationName = "Orofoods.Web.Task17Probe";
    internal const string BlobName = "diagnostics/task17-probe-keys.xml";

    public bool IsAuthorized(string providedKey)
    {
        var configuredKey = configuration["Diagnostics:ProbeKey"];
        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredKey),
            Encoding.UTF8.GetBytes(providedKey));
    }

    public Task<IsolatedDataProtectionProbeResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!IsolatedDataProtectionProbeEndpoint.IsAvailable(environment, configuration))
        {
            return Task.FromResult(IsolatedDataProtectionProbeResult.Unavailable);
        }

        var settings = configuration.GetSection("DataProtection").Get<AppDataProtectionOptions>()?.Azure;
        if (settings is not { Enabled: true } || string.IsNullOrWhiteSpace(settings.BlobUri) || string.IsNullOrWhiteSpace(settings.KeyVaultKeyIdentifier))
        {
            return Task.FromResult(IsolatedDataProtectionProbeResult.Unavailable);
        }

        try
        {
            var credential = new DiagnosticTokenCredential(
                new DefaultAzureCredential(),
                loggerFactory.CreateLogger<DiagnosticTokenCredential>());
            using var provider = providerFactory.Create(
                BuildDiagnosticBlobUri(settings.BlobUri),
                new Uri(settings.KeyVaultKeyIdentifier),
                credential);
            _ = provider.DataProtection.CreateProtector("Task17Diagnostic").Protect("orofoods-task17-probe");
            return Task.FromResult(IsolatedDataProtectionProbeResult.Succeeded);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Isolated data protection probe failed with {ExceptionType}: {Message}",
                exception.GetType().Name,
                AzureIdentityDiagnostics.Redact(exception.Message));
            return Task.FromResult(IsolatedDataProtectionProbeResult.Failed);
        }
    }

    internal static Uri BuildDiagnosticBlobUri(string configuredBlobUri)
    {
        var uri = new Uri(configuredBlobUri, UriKind.Absolute);
        var container = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(container))
        {
            throw new InvalidOperationException("DataProtection:Azure:BlobUri deve incluir o container.");
        }

        return new Uri($"{uri.Scheme}://{uri.Authority}/{container}/{BlobName}");
    }
}

internal interface IIsolatedDataProtectionProviderFactory
{
    IsolatedDataProtectionProviderLease Create(Uri blobUri, Uri keyVaultKeyIdentifier, TokenCredential credential);
}

internal sealed class IsolatedDataProtectionProviderFactory : IIsolatedDataProtectionProviderFactory
{
    public IsolatedDataProtectionProviderLease Create(Uri blobUri, Uri keyVaultKeyIdentifier, TokenCredential credential)
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName(IsolatedDataProtectionProbe.ApplicationName)
            .PersistKeysToAzureBlobStorage(blobUri, credential)
            .ProtectKeysWithAzureKeyVault(keyVaultKeyIdentifier, credential);

        var servicesProvider = services.BuildServiceProvider();
        return new IsolatedDataProtectionProviderLease(
            servicesProvider.GetRequiredService<IDataProtectionProvider>(),
            servicesProvider);
    }
}

internal sealed class IsolatedDataProtectionProviderLease(IDataProtectionProvider dataProtection, IDisposable disposable) : IDisposable
{
    public IDataProtectionProvider DataProtection { get; } = dataProtection;
    public void Dispose() => disposable.Dispose();
}

internal sealed record IsolatedDataProtectionProbeResult(bool Success)
{
    internal static IsolatedDataProtectionProbeResult Succeeded { get; } = new(true);
    internal static IsolatedDataProtectionProbeResult Failed { get; } = new(false);
    internal static IsolatedDataProtectionProbeResult Unavailable { get; } = new(false);
}